using BPUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace OmxPlayerAuto
{
	partial class OmxPlayerAuto : ServiceBase
	{
		private WebServer ws;

		public OmxPlayerAuto()
		{
			Globals.Initialize(System.Reflection.Assembly.GetExecutingAssembly().Location);
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			string settingsPath = Globals.WritableDirectoryBase + "OPA_Settings.cfg";
			Settings settings = new Settings();
			settings.Load(settingsPath);

			ws = new WebServer(settings, settingsPath);
			ws.Start();
		}

		protected override void OnStop()
		{
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
