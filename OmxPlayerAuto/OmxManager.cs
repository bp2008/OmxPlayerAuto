using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;

namespace OmxPlayerAuto
{
	public class OmxManager : IDisposable
	{
		public static HourAndMinute[] staticRestartSchedule = null;
		/// <summary>
		/// Manager ID number.
		/// </summary>
		public readonly int id;
		public readonly string cmd;
		public readonly string url;
		/// <summary>
		/// The omxplayer process which we start(ed) directly.  This process (mostly?) just starts omxplayer.bin which does the real work.  See [binProc].
		/// </summary>
		private Process proc;
		/// <summary>
		/// The omxplayer.bin process which our omxplayer process starts.  This process actually does the work of video decoding.
		/// </summary>
		private Process binProc;
		private PerformanceCounter cpuReader;
		private DateTime lastCpuUsageCalcTime = DateTime.UtcNow;
		//private double lastCpuTotalSeconds = 0;
		private double cpuUsage = 0;
		/// <summary>
		/// The CPU usage of the omxplayer.bin process being managed by this instance.
		/// This value is a percentage, not a simple ratio, and therefore the typical range is (0.0) to (100.0 * number of logical cores).
		/// </summary>
		public double CpuUsage
		{
			get { return cpuUsage; }
		}
		/// <summary>
		/// The thread which manages an omxplayer process.
		/// </summary>
		private Thread thrManager;
		/// <summary>
		/// Starts an omxplayer process with the specified command and begins monitoring it until this instance is Disposed.
		/// </summary>
		/// <param name="id">A number that uniquely identifies this OmxManager.</param>
		/// <param name="cmd">A command string which runs omxplayer.</param>
		public OmxManager(int id, string cmd)
		{
			this.id = id;
			this.cmd = cmd;
			this.url = cmd.Split(' ').LastOrDefault();
			StartThread();
		}

		private void StartThread()
		{
			thrManager?.Abort();
			thrManager = new Thread(BackgroundWork);
			thrManager.Name = "OmxManager " + id + ": " + url;
			thrManager.Start();
		}

		public void BackgroundWork()
		{
			try
			{
				int lastMinute = -1;
				while (true)
				{
					try
					{
						RestartProcess();

						const double LowCpuLimit = 0.1; // CPU usage below this threshold indicates the process is idle.
						const int MaxLowCpuCount = 15; // CPU usage can be below the threshold this many times in a row without consequence.
						int lowCpuCounter = 0;
						while (true)
						{
							if (proc.HasExited)
							{
								Logger.Info("omxplayer exit detected (" + thrManager.Name + ")");
								break;
							}
							if (binProc.HasExited)
							{
								Logger.Info("omxplayer.bin exit detected (" + thrManager.Name + ")");
								break;
							}
							CalcCpuUsage();
							if (cpuUsage < LowCpuLimit)
								lowCpuCounter++;
							else
								lowCpuCounter = 0;
							if (lowCpuCounter > MaxLowCpuCount)
							{
								if (WebServer.settings.ServerSettings.CpuWatchdog)
								{
									Logger.Info("omxplayer.bin hang detected (" + thrManager.Name + ")");
									break;
								}
							}
							DateTime now = DateTime.Now;
							if (now.Minute != lastMinute)
							{
								lastMinute = now.Minute;
								var restartSchedule = staticRestartSchedule;
								if (restartSchedule != null)
								{
									bool doRestart = false;
									foreach (HourAndMinute ham in restartSchedule)
										doRestart |= (ham.Hour == now.Hour && ham.Minute == now.Minute);
									if (doRestart)
										break; // Break, causing process restart, but don't log this because it will happen regularly.
								}
							}

							Thread.Sleep(2000);
						}

						StopProcess();
					}
					catch (ThreadAbortException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex, "OmxManager " + thrManager.Name);
						Thread.Sleep(5000);
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex, "Outer Exception: OmxManager " + thrManager.Name);
			}
		}

		private void RestartProcess()
		{
			StopProcess();
			StartProcess();
			WaitForBinProc();
			CalcCpuUsage();
		}

		private void StopProcess()
		{
			if (binProc != null)
			{
				try
				{
					if (!binProc.HasExited)
						binProc.Kill();
				}
				catch (ThreadAbortException) { throw; }
				catch (Exception ex)
				{
					Logger.Debug(ex, "binProc");
				}
				binProc.Dispose();
				binProc = null;
			}
			if (proc != null)
			{
				try
				{
					if (!proc.HasExited)
						proc.Kill();
				}
				catch (ThreadAbortException) { throw; }
				catch (Exception ex)
				{
					Logger.Debug(ex, "proc");
				}
				proc.Dispose();
				proc = null;
			}
			if (cpuReader != null)
			{
				cpuReader.Dispose();
				cpuReader = null;
			}
		}

		private void StartProcess()
		{
			int idxFirstSpace = cmd.IndexOf(' ');
			string args = null;
			if (idxFirstSpace > -1)
			{
				args = cmd.Substring(idxFirstSpace + 1);
				string exe = cmd.Substring(0, idxFirstSpace);
				proc = Process.Start(exe, args);
			}
			else
				proc = Process.Start(cmd);
		}

		private void WaitForBinProc()
		{
			int pid = proc.Id;
			int tries = 0;
			while (tries++ < 5 && !mappings_binProc.TryGetValue(pid, out binProc))
			{
				if (tries > 1)
					Thread.Sleep(1000);
				ScanOmxPlayerBinProcesses();
			}
			if (binProc == null)
				throw new Exception("Could not find omxplayer.bin started by omxplayer[pid=" + pid + "]");
			cpuReader = new PerformanceCounter("Process", "% Processor Time", binProc.Id.ToString());
		}

		private void CalcCpuUsage()
		{
			DateTime now = DateTime.UtcNow;
			TimeSpan passed = now - lastCpuUsageCalcTime;
			if (passed.TotalSeconds > 0.9)
			{
				cpuUsage = cpuReader.NextValue();
				lastCpuUsageCalcTime = now;

				// binProc.TotalProcessorTime always returns zero even after calling Refresh().  Yay bugs!  So we're using PerformanceCounter instead as seen above.
				//binProc.Refresh();
				//double cpuTotalSeconds = binProc.TotalProcessorTime.TotalSeconds;
				//Console.WriteLine(cpuTotalSeconds + " : " + proc.Id + " : " + binProc.Id + " : " + thrManager.Name);
				//double cpuChange = cpuTotalSeconds - lastCpuTotalSeconds;
				//double secondsPassed = (now - lastCpuUsageCalcTime).TotalSeconds;
				//cpuUsage = cpuChange / secondsPassed;
				//lastCpuTotalSeconds = cpuTotalSeconds;
				//lastCpuUsageCalcTime = now;
			}
		}
		#region (Static/Shared Items) Bin Process Scanning
		private static ConcurrentDictionary<int, Process> mappings_binProc = new ConcurrentDictionary<int, Process>();
		private static DateTime lastBinProcScan = DateTime.MinValue;
		private static void ScanOmxPlayerBinProcesses()
		{
			if ((DateTime.UtcNow - lastBinProcScan).TotalSeconds < 0.9)
				return;
			lock (mappings_binProc)
			{
				if ((DateTime.UtcNow - lastBinProcScan).TotalSeconds < 0.9)
					return;
				lastBinProcScan = DateTime.UtcNow;
				Process[] procs = Process.GetProcessesByName("omxplayer.bin");
				if (procs == null)
					return;
				foreach (Process p in procs)
				{
					int binPid = p.Id;
					int? parentPid = GetParentPid(binPid);
					if (parentPid != null)
						mappings_binProc[parentPid.Value] = p;
				}
			}
		}
		private static int? GetParentPid(int pid)
		{
			try
			{
				string line;
				using (StreamReader reader = new StreamReader("/proc/" + pid + "/stat"))
				{
					line = reader.ReadLine();
				}
				string[] parts = line.Split(new char[] { ' ' }, 5); // Only interested in field at position 3
				if (parts.Length >= 4)
				{
					if (int.TryParse(parts[3], out int ppid))
						return ppid;
				}
			}
			catch (ThreadAbortException) { throw; }
			catch (Exception ex)
			{
				Logger.Debug(ex, "PID: " + pid);
			}
			return null;
		}
		#endregion
		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// dispose managed state (managed objects).
					thrManager?.Abort();
					thrManager = null;
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.
				// set large fields to null.

				disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}
		#endregion
	}
}
