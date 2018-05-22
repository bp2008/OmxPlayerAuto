using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace OmxPlayerAuto
{
	class Program
	{
		static void Main(string[] args)
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
			{
				Console.WriteLine("OS \"" + Environment.OSVersion + "\" is not supported.  This program must be run on a Raspberry Pi.");
				Console.WriteLine("Press any key to exit");
				Console.ReadKey();
				return;
			}
			Logger.logType = LoggingMode.Console | LoggingMode.File;
			Globals.Initialize(System.Reflection.Assembly.GetExecutingAssembly().Location);

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			string settingsPath = Globals.WritableDirectoryBase + "OPA_Settings.cfg";
			Settings settings = new Settings();
			settings.Load(settingsPath);

			Console.WriteLine("OmxPlayerAuto will listen on port " + settings.WebPort + ", configurable in OPA_Settings.cfg");
			WebServer ws = new WebServer(settings, settingsPath);
			ws.Start();
			do
			{
				Console.WriteLine("Type \"exit\" to close");
			}
			while (Console.ReadLine().ToLower() != "exit");
			ws.Stop();

		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is Exception)
				Logger.Debug((Exception)e.ExceptionObject);
			else
				Logger.Debug(e.ExceptionObject.ToString());
		}
	}
}
