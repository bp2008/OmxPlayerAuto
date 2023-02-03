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
using Newtonsoft.Json;

namespace OmxPlayerAuto
{
	public class WebServer : HttpServer
	{
#if DEBUG
		public bool isDebug
		{
			get
			{
				return Debugger.IsAttached;
			}
		}
#else
		public const bool isDebug = false;
#endif
		bool hasKilledOrphanedProcesses = false;
		Thread omxWatcher;
		public static Settings settings;
		DateTime updateCommandsTime = DateTime.UtcNow;
		string settingsPath = "";
		List<OmxManager> allProcessManagers = new List<OmxManager>();
		public WebServer(Settings settings, string settingsPath) : base(settings.WebPort, -1, null)
		{
			WebServer.settings = settings;
			OmxManager.staticRestartSchedule = settings.ServerSettings.GetRestartStreamsSchedule();
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
			//if (!isDebug)
			omxWatcher.Start();
		}
		private string pathMod
		{
			get
			{
				return isDebug ? "../../" : "";
			}
		}
		public override void handleGETRequest(HttpProcessor p)
		{
			if (p.requestedPage == "")
			{
				WriteFile(p, "text/html; charset=utf-8", "default.html");
				return;
			}
			else if (p.requestedPage == "jquery-3.1.1.min.js")
			{
				WriteFile(p, "text/javascript; charset=utf-8", p.requestedPage);
				return;
			}
			else if (p.requestedPage == "getAll")
			{
				AllServerData serverData = new AllServerData();
				serverData.OmxPlayerCommands = string.Join("\n", settings.OmxPlayerCommands);
				serverData.Settings = settings.ServerSettings;
				p.writeSuccess("application/json");
				p.outputStream.Write(JsonConvert.SerializeObject(serverData));
			}
			else if (p.requestedPage == "getStatus")
			{
				p.writeSuccess("text/plain; charset=UTF-8");
				p.outputStream.Write(GetOmxStatus());
			}
		}
		private void WriteFile(HttpProcessor p, string contentType, string name)
		{
			FileInfo fi = new FileInfo(Globals.ApplicationDirectoryBase + pathMod + name);
			if (fi.Exists)
			{
				p.writeSuccess(contentType, contentLength: fi.Length);
				p.outputStream.Flush();
				using (Stream fiStr = fi.OpenRead())
					fiStr.CopyTo(p.tcpStream);
				p.tcpStream.Flush();
			}
			else
				p.writeFailure();
		}
		public class AllServerData
		{
			public string OmxPlayerCommands;
			public ServerSettings Settings;
		}

		private string GetOmxStatus()
		{
			StringBuilder sb = new StringBuilder();
			lock (allProcessManagers)
			{
				foreach (OmxManager manager in allProcessManagers)
				{
					double cpuUsage = manager.CpuUsage;
					string cpuStr = cpuUsage.ToString("0.0").PadLeft(5, ' ') + "%";
					sb.Append("CPU ").Append(cpuStr);
					sb.Append(" [").Append(manager.id.ToString().PadRight((allProcessManagers.Count / 10) + 1, ' ')).Append("] ");
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
				updateCommandsTime = DateTime.UtcNow;
				Logger.Info("Assigned settings.OmxPlayerCommands " + settings.OmxPlayerCommands.Count + " items");
				p.writeSuccess("text/plain; charset=UTF-8");
			}
			else if (p.requestedPage == "setSettings")
			{
				string json = inputData.ReadToEnd();
				settings.ServerSettings = JsonConvert.DeserializeObject<ServerSettings>(json);
				settings.Save(settingsPath);
				OmxManager.staticRestartSchedule = settings.ServerSettings.GetRestartStreamsSchedule();
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
						KillOrphanedProcesses();
						if (settings.ServerSettings.StreamingEnabled)
						{
							hasKilledOrphanedProcesses = false;
							DateTime lastTime = DateTime.UtcNow;
							List<string> cmds = settings.OmxPlayerCommands;
							cmds = cmds.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

							StringBuilder sb = new StringBuilder();
							for (int i = 0; i < cmds.Count; i++)
								sb.Append(Environment.NewLine + i + ": " + cmds[i]);

							Logger.Info(cmds.Count + " items will be started:" + sb.ToString());

							lock (allProcessManagers)
							{
								foreach (string cmd in cmds)
									allProcessManagers.Add(new OmxManager(allProcessManagers.Count, cmd));
							}

							while (settings.ServerSettings.StreamingEnabled && lastTime >= updateCommandsTime)
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
				KillOrphanedProcesses();
			}
		}

		private void KillOrphanedProcesses()
		{
			if (hasKilledOrphanedProcesses)
				return;
			hasKilledOrphanedProcesses = true;

			lock (allProcessManagers)
			{
				foreach (OmxManager manager in allProcessManagers)
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
				allProcessManagers.Clear();
			}

			List<string> cmds = settings.OmxPlayerCommands;
			HashSet<string> processNames = new HashSet<string>(cmds
				.Select(cmd => OmxProcessInfo.GetProcessName(cmd))
				.Where(n => !string.IsNullOrWhiteSpace(n)));

			Console.WriteLine("Killing processes named " + string.Join(", ", processNames.OrderBy(n => n)));

			// Kill orphan processes
			if (processNames.Any(n => n.IEquals("omxplayer")))
				KillAll("omxplayer.bin");

			foreach (string processName in processNames)
			{
				string n = processName;
				if (!Platform.IsUnix())
					n = Path.GetFileNameWithoutExtension(n);
				KillAll(n);
			}
		}
		private void KillAll(string processName)
		{
			try
			{
				Process[] procs = Process.GetProcessesByName(processName);
				if (procs == null)
					return;
				if (procs.Length > 0)
				{
					Console.WriteLine("Killing " + procs.Length + " " + processName + " processes");
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
			string[] parts = StringUtil.ParseCommandLine(cmd);
			if (parts.Length == 1)
				return cmd;
			else
				return UnQuote(parts[parts.Length - 1]);
		}

		private static string UnQuote(string str)
		{
			bool isQuoted = str.StartsWith("\"") && str.EndsWith("\"");
			return isQuoted ? str.Substring(1, str.Length - 2) : str;
		}
		public static string GetProcessName(string cmd)
		{
			string[] parts = StringUtil.ParseCommandLine(cmd);
			string executableFullName = UnQuote(parts[0]);
			return Path.GetFileName(executableFullName);
		}
	}
}
