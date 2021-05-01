using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using XSNotifications;
using XSNotifications.Enum;
using XSNotifications.Helpers;
using XSOverlay_VRChat_Parser.Avalonia;
using XSOverlay_VRChat_Parser.Avalonia.Views;
using XSOverlay_VRChat_Parser.Helpers;
using XSOverlay_VRChat_Parser.Models;

namespace XSOverlay_VRChat_Parser
{
    class Program
    {
        static ConfigurationModel Configuration { get; set; }

        static HashSet<string> IgnorableAudioPaths = new HashSet<string>();
        static HashSet<string> IgnorableIconPaths = new HashSet<string>();

        static string LogFileName { get; set; }
        static string LastKnownLocationName { get; set; } // World name
        static string LastKnownLocationID { get; set; } // World ID

        private static bool hasApplicationMutex = false;
        private static Mutex applicationMutex;
        static readonly object logMutex = new object();

        static Timer LogDetectionTimer { get; set; }

        static Dictionary<string, TailSubscription> Subscriptions { get; set; }

        static DateTime SilencedUntil = DateTime.Now,
                        LastMaximumKeywordsNotification = DateTime.Now;

        static XSNotifier Notifier { get; set; }

        static void Main(string[] args)
        {
            DateTime now = DateTime.Now;

            string versionString = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            string[] versionTokens = versionString.Split('.');

            ConfigurationModel.CurrentVersion = float.Parse($"{versionTokens[0]}.{versionTokens[1]}");

            if (!Directory.Exists(ConfigurationModel.ExpandedUserFolderPath))
            {
                Directory.CreateDirectory(ConfigurationModel.ExpandedUserFolderPath);
                Directory.CreateDirectory($@"{ConfigurationModel.ExpandedUserFolderPath}\Logs");
            }

            LogFileName = $"Session_{now.Year:0000}{now.Month:00}{now.Day:00}{now.Hour:00}{now.Minute:00}{now.Second:00}.log";
            Log(LogEventType.Info, $@"Log initialized at {ConfigurationModel.ExpandedUserFolderPath}\Logs\{LogFileName}");
            Log(LogEventType.Info, $"Current version is v{ConfigurationModel.CurrentVersion:0.00}");

            try
            {
                applicationMutex = new Mutex(true, "XSOVRCParser", out hasApplicationMutex);

                applicationMutex.WaitOne(1000); // Wait around for a second.

                if (!hasApplicationMutex)
                {
                    Log(LogEventType.Error, "Failed to obtain exclusivity. Is another parser instance running?");
                    Exit();
                }
            }
            catch (Exception ex)
            {
                Log(LogEventType.Error, "An exception occurred while attempting to determine exclusivity. Is another parser instance running?");
                Log(ex);
                Exit();
            }

            try
            {
                Configuration = ConfigurationModel.Load();

                // Rewrite configuration to update it with any new fields not in existing configuration. Useful during update process and making sure the config always has updated annotations.
                // Users shouldn't need to re-configure every time they update the software.
                ConfigurationModel.Save(Configuration);
            }
            catch (Exception ex)
            {
                Log(LogEventType.Error, "An exception occurred while attempting to read or write the configuration file.");
                Log(ex);
                Exit();
            }

            IgnorableAudioPaths.Add(string.Empty);
            IgnorableAudioPaths.Add(XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Default));
            IgnorableAudioPaths.Add(XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Warning));
            IgnorableAudioPaths.Add(XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Error));

            IgnorableIconPaths.Add(string.Empty);
            IgnorableIconPaths.Add(XSGlobals.GetBuiltInIconTypeString(XSIconDefaults.Default));
            IgnorableIconPaths.Add(XSGlobals.GetBuiltInIconTypeString(XSIconDefaults.Warning));
            IgnorableIconPaths.Add(XSGlobals.GetBuiltInIconTypeString(XSIconDefaults.Error));

            Subscriptions = new Dictionary<string, TailSubscription>();
            LogDetectionTimer = new Timer(new TimerCallback(LogDetectionTick), null, 0, Configuration.DirectoryPollFrequencyMilliseconds);

            Log(LogEventType.Info, $"Log detection timer initialized with poll frequency {Configuration.DirectoryPollFrequencyMilliseconds} and parse frequency {Configuration.ParseFrequencyMilliseconds}.");

            XSGlobals.DefaultSourceApp = "XSOverlay VRChat Parser";
            XSGlobals.DefaultOpacity = Configuration.Opacity;

            try
            {
                Notifier = new XSNotifier();
            }
            catch (Exception ex)
            {
                Log(LogEventType.Error, "An exception occurred while constructing XSNotifier.");
                Log(ex);
                Exit();
            }

            Log(LogEventType.Info, $"XSNotifier initialized.");

            try
            {
                Notifier.SendNotification(new XSNotification()
                {
                    AudioPath = XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Default),
                    Title = "Application Started",
                    Content = $"VRChat Log Parser has initialized.",
                    Height = 110.0f
                });
            }
            catch (Exception ex)
            {
                Log(LogEventType.Error, "An exception occurred while sending initialization notification.");
                Log(ex);
                Exit();
            }

            UIMain.Start(args, BuildAvaloniaApp(), Configuration); // Blocking call to UI lifecycle
            Exit();
        }

        static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<UIMain>().UsePlatformDetect().LogToTrace()
            .UseReactiveUI();

        static void Exit()
        {
            Log(LogEventType.Info, "Cleaning up before termination.");

            if (Notifier != null)
            {
                Notifier.Dispose();
                Log(LogEventType.Info, "Notifier disposed.");
            }

            if (Subscriptions != null)
            {
                foreach (var item in Subscriptions)
                    item.Value.Dispose();

                Subscriptions.Clear();

                Log(LogEventType.Info, "Subscriptions cleared.");
            }

            if (hasApplicationMutex)
            {
                applicationMutex.ReleaseMutex();
                Log(LogEventType.Info, "Application-level mutex released.");
            }

            Log(LogEventType.Info, "Exiting.");

            Environment.Exit(-1);
        }

        static void Log(LogEventType type, string message, bool uiLogOnly = false)
        {
            DateTime now = DateTime.Now;
            string dateStamp = $"{now.Year:0000}/{now.Month:00}/{now.Day:00}";
            string timeStamp = $"{now.Hour:00}:{now.Minute:00}:{now.Second:00}";
            string typeStamp = $"{(type == LogEventType.Info ? "INFO" : (type == LogEventType.Event ? "EVENT" : "ERROR"))}";

            lock (logMutex)
            {
                MainWindow.EventLogAppend($"[{timeStamp}] <{typeStamp}> {message}\r\n");

                if (!uiLogOnly)
                    File.AppendAllText($@"{ConfigurationModel.ExpandedUserFolderPath}\Logs\{LogFileName}", $"[{dateStamp} {timeStamp}] [{typeStamp}] {message}\r\n");
            }
        }

        static void Log(Exception ex)
        {
            Log(LogEventType.Error, $"{ex.Message}\r\n{ex.InnerException}\r\n{ex.StackTrace}");
        }

        static void SendNotification(XSNotification notification)
        {
            try
            {
                Notifier.SendNotification(notification);
            }
            catch (Exception ex)
            {
                Log(LogEventType.Error, "An exception occurred while sending a routine event notification.");
                Log(ex);
                Exit();
            }
        }

        static void LogDetectionTick(object timerState)
        {
            string[] allFiles = Directory.GetFiles(Environment.ExpandEnvironmentVariables(Configuration.OutputLogRoot));
            foreach (string fn in allFiles)
                if (!Subscriptions.ContainsKey(fn) && fn.Contains("output_log"))
                {
                    Subscriptions.Add(fn, new TailSubscription(fn, ParseTick, 0, Configuration.ParseFrequencyMilliseconds));
                    Log(LogEventType.Info, $"A tail subscription was added to {fn}");
                }
        }

        /// <summary>
        /// This is messy, but they've changed format on me often enough that it's difficult to care!
        /// </summary>
        /// <param name="content"></param>
        static void ParseTick(string content)
        {
            List<Tuple<EventType, XSNotification>> ToSend = new List<Tuple<EventType, XSNotification>>();

            if (!string.IsNullOrWhiteSpace(content))
            {
                string[] lines = content.Split('\n');

                foreach (string dirtyLine in lines)
                {
                    string line = Regex.Replace(dirtyLine
                        .Replace("\r", "")
                        .Replace("\n", "")
                        .Replace("\t", "")
                        .Trim(),
                        @"\s+", " ", RegexOptions.Multiline);

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        int tocLoc = 0;
                        string[] tokens = line.Split(' ');

                        // Get new LastKnownLocationName here
                        if (line.Contains("Joining or"))
                        {
                            for (int i = 0; i < tokens.Length; i++)
                            {
                                if (tokens[i] == "Room:")
                                {
                                    tocLoc = i;
                                    break;
                                }
                            }

                            if (tokens.Length > tocLoc + 1)
                            {
                                string name = "";

                                for (int i = tocLoc + 1; i < tokens.Length; i++)
                                    name += tokens[i] + " ";

                                name = name.Trim();

                                LastKnownLocationName = name.Trim();
                            }
                        }
                        // Get new LastKnownLocationID here
                        else if (line.Contains("Joining w"))
                        {
                            for (int i = 0; i < tokens.Length; i++)
                            {
                                if (tokens[i] == "Joining")
                                {
                                    tocLoc = i;
                                    break;
                                }
                            }

                            if (tokens.Length > tocLoc + 1)
                                LastKnownLocationID = tokens[tocLoc + 1];
                        }
                        // At this point, we have the location name/id and are transitioning.
                        else if (line.Contains("Successfully joined room"))
                        {
                            SilencedUntil = DateTime.Now.AddSeconds(Configuration.WorldJoinSilenceSeconds);

                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.WorldChange, new XSNotification()
                            {
                                Timeout = Configuration.WorldChangedNotificationTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.WorldChangedIconPath) ? Configuration.WorldChangedIconPath : ConfigurationModel.GetLocalResourcePath(Configuration.WorldChangedIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.WorldChangedAudioPath) ? Configuration.WorldChangedAudioPath : ConfigurationModel.GetLocalResourcePath(Configuration.WorldChangedAudioPath),
                                Title = LastKnownLocationName,
                                Content = $"{(Configuration.DisplayJoinLeaveSilencedOverride ? "" : $"Silencing notifications for {Configuration.WorldJoinSilenceSeconds} seconds.")}",
                                Height = 110,
                                Volume = Configuration.WorldChangedNotificationVolume
                            }));

                            Log(LogEventType.Event, $"World changed to {LastKnownLocationName} -> {LastKnownLocationID}");

                            Log(LogEventType.Info, $"http://vrchat.com/home/launch?worldId={LastKnownLocationID.Replace(":", "&instanceId=")}", true);
                        }
                        // Get player joins here
                        else if (line.Contains("[Behaviour] OnPlayerJoined"))
                        {
                            for (int i = 0; i < tokens.Length; i++)
                            {
                                if (tokens[i] == "OnPlayerJoined")
                                {
                                    tocLoc = i;
                                    break;
                                }
                            }

                            string message = "";
                            string displayName = "";

                            if (tokens.Length > tocLoc + 1)
                            {
                                string name = "";

                                for (int i = tocLoc + 1; i < tokens.Length; i++)
                                    name += tokens[i] + " ";

                                displayName = name.Trim();

                                message += displayName;
                            }
                            else
                            {
                                message += "No username was provided.";
                            }

                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.PlayerJoin, new XSNotification()
                            {
                                Timeout = Configuration.PlayerJoinedNotificationTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.PlayerJoinedIconPath) ? Configuration.PlayerJoinedIconPath : ConfigurationModel.GetLocalResourcePath(Configuration.PlayerJoinedIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.PlayerJoinedAudioPath) ? Configuration.PlayerJoinedAudioPath : ConfigurationModel.GetLocalResourcePath(Configuration.PlayerJoinedAudioPath),
                                Title = message,
                                Volume = Configuration.PlayerJoinedNotificationVolume
                            }));

                            Log(LogEventType.Event, $"Join: {message}");
                        }
                        // Get player leaves
                        else if (line.Contains("[Behaviour] OnPlayerLeft "))
                        {
                            for (int i = 0; i < tokens.Length; i++)
                            {
                                if (tokens[i] == "OnPlayerLeft")
                                {
                                    tocLoc = i;
                                    break;
                                }
                            }

                            string message = "";
                            string displayName = "";

                            if (tokens.Length > tocLoc + 1)
                            {
                                string name = "";

                                for (int i = tocLoc + 1; i < tokens.Length; i++)
                                    name += tokens[i] + " ";

                                displayName = name.Trim();

                                message += displayName;
                            }
                            else
                            {
                                message += "No username was provided.";
                            }

                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.PlayerLeft, new XSNotification()
                            {
                                Timeout = Configuration.PlayerLeftNotificationTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.PlayerLeftIconPath) ? Configuration.PlayerLeftIconPath : ConfigurationModel.GetLocalResourcePath(Configuration.PlayerLeftIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.PlayerLeftAudioPath) ? Configuration.PlayerLeftAudioPath : ConfigurationModel.GetLocalResourcePath(Configuration.PlayerLeftAudioPath),
                                Title = message,
                                Volume = Configuration.PlayerLeftNotificationVolume
                            }));

                            Log(LogEventType.Event, $"Leave: {message}");
                        }
                        // Shader keyword limit exceeded
                        else if (line.Contains("Maximum number (256)"))
                        {
                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.KeywordsExceeded, new XSNotification()
                            {
                                Timeout = Configuration.MaximumKeywordsExceededTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.MaximumKeywordsExceededIconPath) ? Configuration.MaximumKeywordsExceededIconPath : ConfigurationModel.GetLocalResourcePath(Configuration.MaximumKeywordsExceededIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.MaximumKeywordsExceededAudioPath) ? Configuration.MaximumKeywordsExceededAudioPath : ConfigurationModel.GetLocalResourcePath(Configuration.MaximumKeywordsExceededAudioPath),
                                Title = "Maximum shader keywords exceeded!",
                                Volume = Configuration.MaximumKeywordsExceededNotificationVolume
                            }));

                            Log(LogEventType.Event, $"Maximum shader keywords exceeded!");
                        }
                        // Portal dropped
                        else if (line.Contains("[Behaviour]") && line.Contains("Portals/PortalInternalDynamic"))
                        {
                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.PortalDropped, new XSNotification()
                            {
                                Timeout = Configuration.PortalDroppedTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.PortalDroppedIconPath) ? Configuration.PortalDroppedIconPath : ConfigurationModel.GetLocalResourcePath(Configuration.PortalDroppedIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.PortalDroppedAudioPath) ? Configuration.PortalDroppedAudioPath : ConfigurationModel.GetLocalResourcePath(Configuration.PortalDroppedAudioPath),
                                Title = "A portal has been spawned.",
                                Volume = Configuration.PortalDroppedNotificationVolume
                            }));

                            Log(LogEventType.Event, $"Portal dropped.");
                        }
                    }
                }
            }

            if (ToSend.Count > 0)
            {
                // Batch joins/leaves to avoid message spam during simultaneous joins/leaves that fall outside of the world change case

                List<Tuple<EventType, XSNotification>> BatchJoins = new List<Tuple<EventType, XSNotification>>();
                List<Tuple<EventType, XSNotification>> BatchLeaves = new List<Tuple<EventType, XSNotification>>();

                int numJoins = 0, numLeaves = 0, batchJoinIndex = 0, batchLeaveIndex = 0;
                List<int> IndicesToRemove = new List<int>();
                for (int i = 0; i < ToSend.Count; i++)
                {
                    if (i > 0 && numJoins == 0 && batchJoinIndex == 0 && ToSend[i].Item1 == EventType.PlayerJoin)
                    {
                        ++numJoins;
                        batchJoinIndex = i;
                    }
                    else if (i > 0 && numLeaves == 0 && batchLeaveIndex == 0 && ToSend[i].Item1 == EventType.PlayerLeft)
                    {
                        ++numLeaves;
                        batchLeaveIndex = i;
                    }
                    else if (numJoins > 0 && ToSend[i].Item1 == EventType.PlayerJoin)
                    {
                        ++numJoins;
                        BatchJoins.Add(ToSend[i]);
                        IndicesToRemove.Add(i);
                    }
                    else if (numLeaves > 0 && ToSend[i].Item1 == EventType.PlayerLeft)
                    {
                        ++numLeaves;
                        BatchLeaves.Add(ToSend[i]);
                        IndicesToRemove.Add(i);
                    }
                }

                if (numJoins > 1)
                {
                    XSNotification thisNotification = ToSend[batchJoinIndex].Item2;
                    thisNotification.Content = $"{thisNotification.Title}, {string.Join(", ", BatchJoins.Select(x => x.Item2.Title))}";
                    thisNotification.Title = $"Group Join: {BatchJoins.Count + 1} users.";
                }

                if (numLeaves > 1)
                {
                    XSNotification thisNotification = ToSend[batchLeaveIndex].Item2;
                    thisNotification.Content = $"{thisNotification.Title}, {string.Join(", ", BatchLeaves.Select(x => x.Item2.Title))}";
                    thisNotification.Title = $"Group Leave: {BatchLeaves.Count + 1} users.";
                }

                if (IndicesToRemove.Any())
                {
                    int[] orderedRemovals = IndicesToRemove.OrderByDescending(x => x).ToArray();

                    for (int i = 0; i < orderedRemovals.Length; i++)
                        ToSend.RemoveAt(orderedRemovals[i]);
                }

                foreach (Tuple<EventType, XSNotification> notification in ToSend)
                {
                    if (
                        (!CurrentlySilenced() && Configuration.DisplayPlayerJoined && notification.Item1 == EventType.PlayerJoin)
                        || (!CurrentlySilenced() && Configuration.DisplayPlayerLeft && notification.Item1 == EventType.PlayerLeft)
                        || (Configuration.DisplayWorldChanged && notification.Item1 == EventType.WorldChange)
                        || (Configuration.DisplayPortalDropped && notification.Item1 == EventType.PortalDropped)
                    )
                        SendNotification(notification.Item2);
                    else if (Configuration.DisplayMaximumKeywordsExceeded && notification.Item1 == EventType.KeywordsExceeded
                        && DateTime.Now > LastMaximumKeywordsNotification.AddSeconds(Configuration.MaximumKeywordsExceededCooldownSeconds))
                    {
                        LastMaximumKeywordsNotification = DateTime.Now;
                        SendNotification(notification.Item2);
                    }
                }
            }
        }

        static bool CurrentlySilenced()
        {
            if (Configuration.DisplayJoinLeaveSilencedOverride)
                return false;

            if (DateTime.Now > SilencedUntil)
                return false;

            return true;
        }

    }
}
