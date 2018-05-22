using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;

namespace OmxPlayerAuto
{
	public class Settings : SerializableObjectBase
	{
		public int WebPort = 80;
		public List<string> OmxPlayerCommands = new List<string>();
		public ServerSettings ServerSettings = new ServerSettings();
	}
	public class ServerSettings
	{
		public bool StreamingEnabled = false;
		public bool CpuWatchdog = false;
		public string RestartStreamsSchedule = "600 1800";
		public HourAndMinute[] GetRestartStreamsSchedule()
		{
			if (RestartStreamsSchedule == null)
				return new HourAndMinute[0];
			return RestartStreamsSchedule.Split(' ').Select(s =>
			{
				if (int.TryParse(s, out int i))
				{
					int hour = i / 100;
					int minute = i % 100;
					if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
						return null;
					return new HourAndMinute(hour, minute);
				}
				return null;
			}).Where(h => h != null).ToArray();
		}
	}
	public class HourAndMinute
	{
		public int Hour;
		public int Minute;
		public HourAndMinute()
		{
		}
		public HourAndMinute(int Hour, int Minute)
		{
			this.Hour = Hour;
			this.Minute = Minute;
		}
		public override string ToString()
		{
			return Hour + ":" + Minute;
		}
	}
}
