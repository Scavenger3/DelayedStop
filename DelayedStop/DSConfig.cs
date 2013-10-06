using System;
using System.IO;
using Newtonsoft.Json;

namespace DelayedStop
{
	public class DSConfig
	{
		public string ChatPrefix = "[Warning] ";
		public string TimeLeft_Message = "The server will restart in @time-left@";
		public string ShutdownPaused_Message = "Server restart has been paused!";
		public string ShutdownResumed_Message = "Server restart has been resumed with @time-left@ left";
		public string ShutdownCancelled_Message = "Server restart has been cancelled!";
		public string Shutdown_Message = "Server is shutting down!";
		public string NotifyAt = "10m,5m,1m,30s,10s";
		public byte ColorR = 173;
		public byte ColorG = 255;
		public byte ColorB = 47;

		public DSConfig Write(string file)
		{
			File.WriteAllText(file, JsonConvert.SerializeObject(this, Formatting.Indented));
			return this;
		}

		public static DSConfig Read(string file)
		{
			if (!File.Exists(file))
			{
				DSConfig.WriteExample(file);
			}
			return JsonConvert.DeserializeObject<DSConfig>(File.ReadAllText(file));
		}

		public static void WriteExample(string file)
		{
			File.WriteAllText(file, @"{
  ""ChatPrefix"": ""[Warning] "",
  ""TimeLeft_Message"": ""The server will restart in @time-left@"",
  ""ShutdownPaused_Message"": ""Server restart has been paused!"",
  ""ShutdownResumed_Message"": ""Server restart has been resumed with @time-left@ left"",
  ""ShutdownCancelled_Message"": ""Server restart has been cancelled!"",
  ""Shutdown_Message"": ""Server is shutting down!"",
  ""NotifyAt"": ""10m,5m,1m,30s,10s"",
  ""ColorR"": 173,
  ""ColorG"": 255,
  ""ColorB"": 47
}");
		}
	}
}
