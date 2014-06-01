using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace DelayedStop
{
	[ApiVersion(1, 16)]
	public class DelayedStop : TerrariaPlugin
	{
		public override string Name { get { return "Delayed Stop"; } }
		public override string Author { get { return "Scavenger"; } }
		public override string Description { get { return "Stop your server in x seconds"; } }
		public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

		public DSConfig Config = new DSConfig();
		public string ConfigPath { get { return Path.Combine(TShock.SavePath, "DelayedStopConfig.json"); } }
		public bool ShutdownInProgress = false;
		public bool ShutdownPaused = false;
		public string ShutdownReason = string.Empty;
		public int TimeLeft = 0;
		public int[] NotifyIntervals = new int[0];
		public DateTime LastCheck = DateTime.UtcNow;

		public DelayedStop(Main game) : base(game) { }

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		public void OnInitialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("dstop.shutdown", dstop, "dstop"));
			Commands.ChatCommands.Add(new Command("dstop.info", dinfo, "dinfo"));
			LoadConfig();
		}

		protected override void Dispose(bool Disposing)
		{
			if (Disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
			}
			base.Dispose(Disposing);
		}

		public void LoadConfig()
		{
			try
			{
				Config = DSConfig.Read(ConfigPath).Write(ConfigPath);
				ParseNotifyIntervals();
			}
			catch (Exception ex)
			{
				Log.ConsoleError("[Delayed Stop] An exception occoured while parsing the config, check the logs for more details!");
				Log.Error(ex.ToString());
				Config = new DSConfig();
			}
		}
		public void ReloadConfig(CommandArgs args)
		{
			try
			{
				Config = DSConfig.Read(ConfigPath).Write(ConfigPath);
				ParseNotifyIntervals();
				args.Player.SendSuccessMessage("Reloaded DelayedStop Config!");
			}
			catch (Exception ex)
			{
				args.Player.SendErrorMessage("[Delayed Stop] An exception occoured while parsing the config, check the logs for more details!");
				Log.Error("[Delayed Stop] An exception occoured while parsing the config, check the logs for more details!\n" + ex.ToString());
				Config = new DSConfig();
			}
		}
		public void ParseNotifyIntervals()
		{
			string[] NewIntervals = Config.NotifyAt.Split(',');
			this.NotifyIntervals = new int[NewIntervals.Length];
			for (int i = 0; i < NewIntervals.Length; i++)
			{
				var Interval = NewIntervals[i].Trim().ToLower();
				if (Interval.EndsWith("s") && !Interval.Contains("m"))
				{
					this.NotifyIntervals[i] = int.Parse(Interval.TrimEnd('s'));
				}
				else if (Interval.EndsWith("m") && !Interval.Contains("s"))
				{
					this.NotifyIntervals[i] = int.Parse(Interval.TrimEnd('m')) * 60;
				}
				else if (Interval.Contains("m") && Interval.EndsWith("s"))
				{
					var MinutesAndSeconds = Interval.Split('m');
					this.NotifyIntervals[i] = (int.Parse(MinutesAndSeconds[0]) * 60) + int.Parse(MinutesAndSeconds[1].TrimEnd('s'));
				}
				else
				{
					this.NotifyIntervals[i] = int.Parse(Interval);
				}
			}
		}

		public string ParseTimeLeftMessage()
		{
			return Config.TimeLeft_Message.Replace("@time-left@", FormatTime(TimeLeft));
		}

		public string ParsePausedMessage()
		{
			return Config.ShutdownPaused_Message.Replace("@time-left@", FormatTime(TimeLeft));
		}

		public string ParseResumedMessage()
		{
			return Config.ShutdownResumed_Message.Replace("@time-left@", FormatTime(TimeLeft));
		}
		public static string FormatTime(int time)
		{
			int Minutes = (time / 60);
			int Seconds = (time - (Minutes * 60));
			if (Minutes == 0)
			{
				if (Seconds > 1 || Seconds == 0)
					return Seconds + " Seconds";
				else
					return Seconds + " Second";
			}
			else
			{
				if (Seconds == 0)
				{
					if (Minutes > 1)
						return Minutes + " Minutes";
					else
						return Minutes + " Minute";
				}
				else
				{
					if (Minutes > 1 && Seconds > 1)
						return Minutes + " Minutes and " + Seconds + " Seconds";
					else if (Minutes == 1 && Seconds > 1)
						return Minutes + " Minute and " + Seconds + " Seconds";
					else
						return Minutes + " Minute and " + Seconds + " Second";
				}
			}
		}

		public void OnUpdate(EventArgs args)
		{
			if (ShutdownInProgress && !ShutdownPaused && (DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
			{
				LastCheck = DateTime.UtcNow;
				if (this.NotifyIntervals.Contains(TimeLeft))
				{
					TShock.Utils.Broadcast(string.Concat(Config.ChatPrefix, ParseTimeLeftMessage(), ShutdownReason), Config.ColorR, Config.ColorG, Config.ColorB);
				}
				if (TimeLeft < 1)
				{
					TShock.Utils.StopServer(true, string.Concat(Config.Shutdown_Message, ShutdownReason));
				}
				TimeLeft--;
			}
		}

		public void OnLeave(LeaveEventArgs args)
		{
			if (ShutdownInProgress && !ShutdownPaused)
			{
				CheckOnline(args.Who);
			}
		}
		public void CheckOnline(int Ignore = -1)
		{
			var HasPlayersOnline = false;
			for (int i = 0; i < TShock.Players.Length; i++)
			{
				if (i != Ignore && TShock.Players[i] != null && TShock.Players[i].Active)
				{
					HasPlayersOnline = true;
					break;
				}
			}
			if (!HasPlayersOnline)
			{
				Log.ConsoleInfo("[Delayed Stop] A delayed stop is in progress and no one is on the server!");
				Log.ConsoleInfo("[Delayed Stop] Server will now shutdown!");
				TShock.Utils.StopServer(true, string.Concat(Config.Shutdown_Message, ShutdownReason));
			}
		}

		public void dstop(CommandArgs args)
		{
			var SubCommand = args.Parameters.Count > 0 ? args.Parameters[0].ToLower() : string.Empty;
			int ShutdownDelay;
			if (int.TryParse(SubCommand, out ShutdownDelay))
			{
				if (ShutdownInProgress)
				{
					args.Player.SendErrorMessage("A delayed stop is already in progress!");
					return;
				}
				ShutdownReason = string.Empty;
				if (args.Parameters.Count > 1)
				{
					args.Parameters.RemoveAt(0);
					ShutdownReason = " ({0})".SFormat(string.Join(" ", args.Parameters));
				}
				LastCheck = DateTime.UtcNow;
				TimeLeft = ShutdownDelay;
				ShutdownInProgress = true;
				args.Player.SendSuccessMessage("A delayed stop has been started!");
				CheckOnline(); // Incase the console sends this command
				return;
			}
			switch (SubCommand)
			{
				case "pause":
				case "wait":
					{
						if (!ShutdownInProgress)
						{
							args.Player.SendErrorMessage("A delayed stop is not in progress!");
							return;
						}
						if (ShutdownPaused)
						{
							args.Player.SendErrorMessage("The delayed stop is already paused!");
							return;
						}
						ShutdownPaused = true;
						TShock.Utils.Broadcast(string.Concat(Config.ChatPrefix, ParsePausedMessage()), Config.ColorR, Config.ColorG, Config.ColorB);
					}
					break;
				case "resume":
				case "go":
				case "continue":
					{
						if (!ShutdownInProgress)
						{
							args.Player.SendErrorMessage("A delayed stop is not in progress!");
							return;
						}
						if (!ShutdownPaused)
						{
							args.Player.SendErrorMessage("The delayed stop is not paused!");
							return;
						}
						ShutdownPaused = false;
						TShock.Utils.Broadcast(string.Concat(Config.ChatPrefix, ParseResumedMessage()), Config.ColorR, Config.ColorG, Config.ColorB);
					}
					break;
				case "cancel":
				case "stop":
					{
						if (!ShutdownInProgress)
						{
							args.Player.SendErrorMessage("A delayed stop is not in progress!");
							return;
						}
						ShutdownInProgress = false;
						ShutdownPaused = false;
						ShutdownReason = string.Empty;
						TimeLeft = 0;
						TShock.Utils.Broadcast(string.Concat(Config.ChatPrefix, Config.ShutdownCancelled_Message), Config.ColorR, Config.ColorG, Config.ColorB);
					}
					break;
				case "info":
				case "debug":
					{
						args.Player.SendInfoMessage("Shutdown in progress: {0}".SFormat(ShutdownInProgress));
						args.Player.SendInfoMessage("Shutdown Paused: {0}".SFormat(ShutdownPaused));
						args.Player.SendInfoMessage("Time left till shutdown: {0}".SFormat(FormatTime(TimeLeft)));
						args.Player.SendInfoMessage("Reason for shutdown:{0}".SFormat(ShutdownReason));
						args.Player.SendInfoMessage("Notify At: {0}".SFormat(Config.NotifyAt));
					}
					break;
				case "reload":
					{
						ReloadConfig(args);
					}
					break;
				case "help":
					{
						args.Player.SendInfoMessage("/dstop <seconds> [reason] - Stops the server in <seconds> seconds");
						args.Player.SendInfoMessage("/dstop pause - Pauses the countdown");
						args.Player.SendInfoMessage("/dstop resume - Resumes the paused countdown");
						args.Player.SendInfoMessage("/dstop cancel - Cancels the delayed server shutdown");
						args.Player.SendInfoMessage("/dstop reload - reloads settings from config file");
					}
					break;
				default:
					{
						args.Player.SendWarningMessage("Usage: /dstop <seconds> [reason] - Stops the server in <seconds> seconds");
						args.Player.SendWarningMessage("Usage: /dstop help - Shows more /dstop commands");
					}
					break;
			}
		}

		public void dinfo(CommandArgs args)
		{
			args.Player.SendInfoMessage("Shutdown in progress: {0}".SFormat(ShutdownInProgress));
			args.Player.SendInfoMessage("Shutdown Paused: {0}".SFormat(ShutdownPaused));
			args.Player.SendInfoMessage("Time left till shutdown: {0}".SFormat(FormatTime(TimeLeft)));
			args.Player.SendInfoMessage("Reason for shutdown:{0}".SFormat(ShutdownReason));
			args.Player.SendInfoMessage("Notify At: {0}".SFormat(Config.NotifyAt));
		}
	}
}