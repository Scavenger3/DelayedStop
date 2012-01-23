using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;
using Config;
using System.Timers;

namespace DelayedStop
{
    [APIVersion(1, 11)]
    public class DelayedStop : TerrariaPlugin
    {
        public static dsConfig getConfig { get; set; }
        internal static string getConfigPath { get { return Path.Combine(TShock.SavePath, "DelayedStopConfig.json"); } }
        public static bool shutdowninprogress = false;
        public static bool shutdownpaused = false;
        public static bool noconferr = true;
        public static string shutdownreason = "";
        public static int timeleft = 0;
        public static CommandArgs whostarted;
        public static Timer ds = new Timer(1000);

        public override string Name
        {
            get { return "Delayed Stop"; }
        }

        public override string Author
        {
            get { return "by Scavenger"; }
        }

        public override string Description
        {
            get { return "Stop your server in X seconds"; }
        }

        public override Version Version
        {
            get { return new Version("1.2.2"); }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                GameHooks.Initialize -= OnInitialize;
            base.Dispose(disposing);
        }

        public DelayedStop(Main game)
            : base(game)
        {
            getConfig = new dsConfig();
        }
        

        #region Config

        public static void SetupConfig()
        {
            try
            {
                if (File.Exists(getConfigPath))
                {
                    getConfig = dsConfig.Read(getConfigPath);
                    char[] ctoremove = { 's', 'm' };
                    string[] cstrNotifiers = getConfig.NotifyAt.Split(',');
                    foreach (string ctimeint in cstrNotifiers)
                    {
                        int cnumsecs = 0;
                        string[] cmands = ctimeint.Split('m');
                        bool cnotifiersnos = int.TryParse(ctimeint, out cnumsecs);

                        if (ctimeint.Contains("s") && !ctimeint.Contains("m"))
                            cnumsecs = int.Parse(ctimeint.TrimEnd(ctoremove));
                        else if (ctimeint.Contains("m") && !ctimeint.Contains("s"))
                            cnumsecs = int.Parse(ctimeint.TrimEnd(ctoremove)) * 60;
                        else if (ctimeint.Contains("m") && ctimeint.Contains("s"))
                            cnumsecs = int.Parse(cmands[1].TrimEnd(ctoremove)) + (int.Parse(cmands[0].TrimEnd(ctoremove)) * 60);
                    }
                    noconferr = true;
                }
                getConfig.Write(getConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in Delayed Stop's config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("Config Exception in Delayed Stop's Config file");
                Log.Error(ex.ToString());
                noconferr = false;
            }
        }
        #endregion Config

        #region Config Reload

        public static void ReloadConfig(CommandArgs sendto)
        {
            try
            {
                if (File.Exists(getConfigPath))
                {
                    getConfig = dsConfig.Read(getConfigPath);
                    char[] ctoremove = { 's', 'm' };
                    string[] cstrNotifiers = getConfig.NotifyAt.Split(',');
                    foreach (string ctimeint in cstrNotifiers)
                    {
                        int cnumsecs = 0;
                        string[] cmands = ctimeint.Split('m');
                        bool cnotifiersnos = int.TryParse(ctimeint, out cnumsecs);

                        if (ctimeint.Contains("s") && !ctimeint.Contains("m"))
                            cnumsecs = int.Parse(ctimeint.TrimEnd(ctoremove));
                        else if (ctimeint.Contains("m") && !ctimeint.Contains("s"))
                            cnumsecs = int.Parse(ctimeint.TrimEnd(ctoremove)) * 60;
                        else if (ctimeint.Contains("m") && ctimeint.Contains("s"))
                            cnumsecs = int.Parse(cmands[1].TrimEnd(ctoremove)) + (int.Parse(cmands[0].TrimEnd(ctoremove)) * 60);
                    }
                    noconferr = true;
                    sendto.Player.SendMessage("Config file reloaded sucessfully!", Color.Green);
                }
                getConfig.Write(getConfigPath);
            }
            catch (Exception ex)
            {
                sendto.Player.SendMessage("Error in config file! Check log for more details.", Color.Red);
                Log.Error("Config Exception in Delayed Stop's Config file");
                Log.Error(ex.ToString());
                noconferr = false;
            }
        }
        #endregion Config Reload

        #region display time left

        public static string dispTimeLeft()
        {
            int Minutes = (timeleft / 60);
            int Seconds = (timeleft - (Minutes * 60));
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
        #endregion display time left

        #region get message
        public static string getmessage()
        {
            string t = "";

            if (getConfig.TimeLeft_Message == "" || !getConfig.TimeLeft_Message.Contains("@time-left@"))
                t = "The server will restart in @time-left@";
            else
                t = getConfig.TimeLeft_Message;

            t = t.Replace("@time-left@", dispTimeLeft());
            return t;

        }
        #endregion get message

        #region timer elapse
        static void ds_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!shutdowninprogress)
                ds.Stop();
            else if (shutdowninprogress && !shutdownpaused && noconferr)
            {
                char[] toremove = { 's', 'm' };
                string[] strNotifiers = getConfig.NotifyAt.Split(',');
                foreach (string timeint in strNotifiers)
                {
                    int numsecs = 0;
                    string[] mands = timeint.Split('m');
                    bool notifiersnos = int.TryParse(timeint, out numsecs);

                    if (timeint.Contains("s") && !timeint.Contains("m"))
                        numsecs = int.Parse(timeint.TrimEnd(toremove));
                    else if (timeint.Contains("m") && !timeint.Contains("s"))
                        numsecs = int.Parse(timeint.TrimEnd(toremove)) * 60;
                    else if (timeint.Contains("m") && timeint.Contains("s"))
                        numsecs = int.Parse(mands[1].TrimEnd(toremove)) + (int.Parse(mands[0].TrimEnd(toremove)) * 60);

                    if (timeleft == numsecs)
                        TShock.Utils.Broadcast(getConfig.ChatPrefix + " " + getmessage() + shutdownreason, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
                }
                timeleft--;
                if (timeleft == 0)
                {
                    TShock.Utils.ForceKickAll(getConfig.Shutdown_Message + shutdownreason);
                    WorldGen.saveWorld();
                    Netplay.disconnect = true;
                }
            }
        }
        #endregion

        public void OnInitialize()
        {
            SetupConfig();
            Commands.ChatCommands.Add(new Command("delayedstop", dstop, "dstop"));
            Commands.ChatCommands.Add(new Command("dsinfo", dinfo, "dinfo"));
        }

        public static void dstop(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Usage: /dstop <seconds> [reason] - Stops the server in <seconds> seconds", Color.Red);
                args.Player.SendMessage("Usage: /dstop help - Shows more dstop commands", Color.Red);
                return;
            }

            string subcmd = args.Parameters[0];
            int timetillshutdown = 0;
            bool subcmdIsNum = int.TryParse(subcmd, out timetillshutdown);

            if (subcmd.ToLower() == "pause" || subcmd.ToLower() == "wait")
            {
                if (shutdownpaused == false && shutdowninprogress == true)
                {
                    shutdownpaused = true;
                    TShock.Utils.Broadcast(getConfig.ChatPrefix + " " + getConfig.ShutdownPaused_Message, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
                }
                else
                {
                    args.Player.SendMessage("Error: No Delayed Shutdown in progress!", Color.Red);
                }
            }
            else if (subcmd.ToLower() == "resume" || subcmd.ToLower() == "go" || subcmd.ToLower() == "continue")
            {
                if (shutdownpaused == true && shutdowninprogress == true)
                {
                    shutdownpaused = false;
                    TShock.Utils.Broadcast(getConfig.ChatPrefix + " " + getConfig.ShutdownResumed_Message, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
                }
                else
                {
                    args.Player.SendMessage("Error: No Delayed Shutdown in progress!", Color.Red);
                }
            }
            else if (subcmd.ToLower() == "cancel" || subcmd.ToLower() == "stop")
            {
                if (shutdowninprogress == true)
                {
                    shutdowninprogress = false;
                    shutdownpaused = false;
                    timeleft = 0;
                    shutdownreason = "";
                    TShock.Utils.Broadcast(getConfig.ChatPrefix + " " + getConfig.ShutdownCancelled_Message, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
                }
                else
                    args.Player.SendMessage("Error: No Delayed Shutdown in progress!", Color.Red);
            }
            else if (subcmd.ToLower() == "help")
            {
                args.Player.SendMessage("/dstop <seconds> [reason] - Stops the server in <seconds> seconds", Color.IndianRed);
                args.Player.SendMessage("/dstop pause - Pauses the countdown", Color.IndianRed);
                args.Player.SendMessage("/dstop resume - Resumes the paused countdown", Color.IndianRed);
                args.Player.SendMessage("/dstop cancel - Cancels the delayed server shutdown", Color.IndianRed);
                args.Player.SendMessage("/dstop reload - reloads settings from config file", Color.IndianRed);
            }
            else if (subcmdIsNum)
            {
                if (shutdowninprogress == false && noconferr)
                {
                    if (args.Parameters.Count >= 2)
                    {
                        foreach (string resonword in args.Parameters)
                        {
                            shutdownreason = shutdownreason + resonword + " ";
                        }
                        shutdownreason = shutdownreason.Remove(0, subcmd.Length + 1);
                        shutdownreason = " (" + shutdownreason.Remove(shutdownreason.Length - 1) + ")";
                    }
                    whostarted = args;
                    timeleft = timetillshutdown;
                    shutdowninprogress = true;

                    ds.Elapsed += new ElapsedEventHandler(ds_Elapsed);
                    ds.Start();
                }
                else if (!noconferr)
                    args.Player.SendMessage("Error: Cannot start because of error in config file!", Color.Red);
                else
                    args.Player.SendMessage("Error: Delayed shutdown already in progress!", Color.Red);
            }
            else if (subcmd.ToLower() == "reload")
            {
                if (shutdowninprogress == false)
                {
                    ReloadConfig(args);
                }
                else
                {
                    args.Player.SendMessage("Error: Cannot reload while shutdown in progress!", Color.Red);
                }
            }
            else if (subcmd.ToLower() == "info" || subcmd.ToLower() == "debug")
            {
                args.Player.SendMessage("Shutdown in progress: " + shutdowninprogress, Color.IndianRed);
                args.Player.SendMessage("Shutdown Paused: " + shutdownpaused, Color.IndianRed);
                args.Player.SendMessage("Time left till shutdown: " + dispTimeLeft(), Color.IndianRed);
                args.Player.SendMessage("Reason for shutdown:" + shutdownreason, Color.IndianRed);
                args.Player.SendMessage("Notify At: " + getConfig.NotifyAt, Color.IndianRed);
            }
            else
            {
                args.Player.SendMessage("Usage: /dstop <seconds> [reason] - Stops the server in <seconds> seconds", Color.Red);
                args.Player.SendMessage("Usage: /dstop help - Shows more dstop commands", Color.Red);
            }
        }
        public static void dinfo(CommandArgs args)
        {
            args.Player.SendMessage("Shutdown in progress: " + shutdowninprogress, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
            args.Player.SendMessage("Shutdown Paused: " + shutdownpaused, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
            args.Player.SendMessage("Time left till shutdown: " + dispTimeLeft(), getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
            args.Player.SendMessage("Reason for shutdown:" + shutdownreason, getConfig.ColorR, getConfig.ColorG, getConfig.ColorB);
        }
    }
}

namespace Config
{
    public class dsConfig
    {
        public string ChatPrefix = "[Warning]";
        public string TimeLeft_Message = "The server will restart in @time-left@";
        public string ShutdownPaused_Message = "Server restart is paused";
        public string ShutdownResumed_Message = "Server restart is resumed";
        public string ShutdownCancelled_Message = "Server restart is cancelled!";
        public string Shutdown_Message = "Server is shutting down!";
        public string NotifyAt = "10m,5m,1m,30s,10s";
        public byte ColorR = 173;
        public byte ColorG = 255;
        public byte ColorB = 47;

        public static dsConfig Read(string path)
        {
            if (!File.Exists(path))
                return new dsConfig();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static dsConfig Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<dsConfig>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }
        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<dsConfig> ConfigRead;
    }
}