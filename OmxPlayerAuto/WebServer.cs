using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.SimpleHttp;

namespace OmxPlayerAuto
{
	public class WebServer : HttpServer
	{
#if DEBUG
		public const bool isDebug = true;
#else
		public const bool isDebug = false;
#endif
		Thread omxWatcher;
		Settings settings;
		DateTime updateSettingsTime = DateTime.UtcNow;
		string settingsPath = "";
		List<OmxManager> allOmx = new List<OmxManager>();
		public WebServer(Settings settings, string settingsPath) : base(settings.WebPort, -1, null)
		{
			this.settings = settings;
			this.settingsPath = settingsPath;
			if (settings.OmxPlayerCommands == null)
				settings.OmxPlayerCommands = new List<string>();
			if (settings.OmxPlayerCommands.Count == 0)
			{
				settings.OmxPlayerCommands.Add("omxplayer"
					+ " --lavfdopts probesize:25000"
					+ " --no-keys"
					+ " --live"
					+ " --timeout 30"
					+ " --aspect-mode stretch"
					+ " --layer 2"
					+ " --nohdmiclocksync"
					+ " --avdict rtsp_transport:tcp"
					+ " --win \"0 0 960 540\""
					+ " \"rtsp://127.0.0.1/\"");
				settings.Save(settingsPath);
			}

			Logger.Info("Starting Service.");

			omxWatcher = new Thread(omxWatcherLoop);
			omxWatcher.Name = "OMXPlayer Watcher Thread";
			if (!isDebug)
				omxWatcher.Start();
		}

		public override void handleGETRequest(HttpProcessor p)
		{
			if (p.requestedPage == "")
			{
				p.writeSuccess();
				p.outputStream.Write(File.ReadAllText(isDebug ? "../../default.html" : (Globals.ApplicationDirectoryBase + "default.html"), Encoding.UTF8));
			}
			else if (p.requestedPage == "jquery-3.1.1.min.js")
			{
				FileInfo fi = new FileInfo(isDebug ? "../../jquery-3.1.1.min.js" : (Globals.ApplicationDirectoryBase + "jquery-3.1.1.min.js"));
				if (fi.Exists)
				{
					p.writeSuccess("text/javascript; charset=UTF-8", contentLength: fi.Length);
					p.outputStream.Flush();
					using (Stream fiStr = fi.OpenRead())
						fiStr.CopyTo(p.rawOutputStream);
					p.rawOutputStream.Flush();
				}
				return;
			}
			else if (p.requestedPage == "getList")
			{
				List<KeyValuePair<string, string>> additionalHeaders = new List<KeyValuePair<string, string>>();
				additionalHeaders.Add(new KeyValuePair<string, string>("Streamingenabled", settings.StreamingEnabled ? "1" : "0"));
				p.writeSuccess("text/plain; charset=UTF-8", additionalHeaders: additionalHeaders);
				p.outputStream.Write(string.Join("\n", settings.OmxPlayerCommands));
			}
			else if (p.requestedPage == "setEnabled")
			{
				settings.StreamingEnabled = p.GetBoolParam("enable");
				settings.Save(settingsPath);
				p.writeSuccess("text/plain; charset=UTF-8");
			}
			else if (p.requestedPage == "getEnabled")
			{
				p.writeSuccess("text/plain; charset=UTF-8");
				p.outputStream.Write(settings.StreamingEnabled ? "1" : "0");
			}
			else if (p.requestedPage == "getStatus")
			{
				p.writeSuccess("text/plain; charset=UTF-8");
				p.outputStream.Write(GetOmxStatus());
			}
		}

		private string GetOmxStatus()
		{
			StringBuilder sb = new StringBuilder();
			lock (allOmx)
			{
				foreach (OmxManager manager in allOmx)
				{
					double cpuUsage = manager.CpuUsage;
					string cpuStr = cpuUsage.ToString("0.0").PadLeft(5, ' ') + "%";
					sb.Append("CPU ").Append(cpuStr);
					sb.Append(" [").Append(manager.id.ToString().PadRight((allOmx.Count / 10) + 1, ' ')).Append("] ");
					sb.Append(OmxProcessInfo.GetURL(manager.url)).Append("\n");
				}
			}
			return sb.ToString();
		}

		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			if (p.requestedPage == "setNewList")
			{
				string newList = p.GetPostParam("newlist");
				settings.OmxPlayerCommands = new List<string>(newList.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
				settings.Save(settingsPath);
				updateSettingsTime = DateTime.UtcNow;
				Logger.Info("Assigned settings.OmxPlayerCommands " + settings.OmxPlayerCommands.Count + " items");
				p.writeSuccess("text/plain; charset=UTF-8");
			}
		}
		public void omxWatcherLoop()
		{
			try
			{
				while (true)
				{
					try
					{
						KillAllOmxPlayers();
						if (settings.StreamingEnabled)
						{
							DateTime lastTime = DateTime.UtcNow;
							List<string> cmds = settings.OmxPlayerCommands;
							cmds = cmds.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

							StringBuilder sb = new StringBuilder();
							for (int i = 0; i < cmds.Count; i++)
								sb.Append(Environment.NewLine + i + ": " + cmds[i]);

							Logger.Info(cmds.Count + " items will be started:" + sb.ToString());

							lock (allOmx)
							{
								foreach (string cmd in cmds)
									allOmx.Add(new OmxManager(allOmx.Count, cmd));
							}

							while (settings.StreamingEnabled && lastTime >= updateSettingsTime)
								Thread.Sleep(1000);
						}
						else
							Thread.Sleep(2500);
					}
					catch (ThreadAbortException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex);
						Thread.Sleep(5000);
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
			finally
			{
				KillAllOmxPlayers();
			}
		}

		private void KillAllOmxPlayers()
		{
			if (allOmx.Count == 0)
				return;
			lock (allOmx)
			{
				foreach (OmxManager manager in allOmx)
				{
					try
					{
						manager.Dispose();
					}
					catch (ThreadAbortException) { throw; }
					catch (Exception ex)
					{
						Logger.Debug(ex);
					}
				}
				allOmx.Clear();
			}

			// Kill all omxplayer.bin processes, because the Manager instances don't necessarily know about their bin processes yet when they are disposed.
			try
			{
				Process[] procs = Process.GetProcessesByName("omxplayer.bin");
				if (procs == null)
					return;
				if (procs.Length > 0)
				{
					Console.WriteLine("Killing " + procs.Length + " omxplayer.bin processes");
					foreach (Process p in procs)
						p.Kill();
				}
			}
			catch (ThreadAbortException) { throw; }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}


		protected override void stopServer()
		{
			omxWatcher.Abort();
		}
	}
	class OmxProcessInfo
	{
		public string cmd;
		public Process proc;
		public int pid;
		private DateTime lastCpuUsageCalcTime = DateTime.UtcNow;
		private double lastCpuTotalSeconds = 0;
		public double cpuUsage = 0;
		public OmxProcessInfo(string cmd, Process proc)
		{
			this.cmd = cmd;
			this.proc = proc;
			pid = proc.Id;
		}

		public double CalcCpuUsage()
		{
			DateTime now = DateTime.UtcNow;
			TimeSpan passed = now - lastCpuUsageCalcTime;
			if (passed.TotalSeconds > 0.9)
			{
				double cpuTotalSeconds = proc.TotalProcessorTime.TotalSeconds;
				double cpuChange = cpuTotalSeconds - lastCpuTotalSeconds;
				double secondsPassed = (now - lastCpuUsageCalcTime).TotalSeconds;
				cpuUsage = cpuChange / secondsPassed;
				lastCpuUsageCalcTime = now;
				lastCpuTotalSeconds = cpuTotalSeconds;
			}
			return cpuUsage;
		}
		public string GetURL()
		{
			return GetURL(cmd);
		}
		public static string GetURL(string cmd)
		{
			return cmd.Split(' ').LastOrDefault();
		}
	}
}
