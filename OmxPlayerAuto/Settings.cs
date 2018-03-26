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
		public bool StreamingEnabled = false;
	}
}
