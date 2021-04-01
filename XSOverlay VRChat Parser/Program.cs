using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XSNotifications;
using XSNotifications.Enum;
using XSNotifications.Helpers;
using XSOverlay_VRChat_Parser.Helpers;
using XSOverlay_VRChat_Parser.Models;

namespace XSOverlay_VRChat_Parser
{
    class Program
    {
        static ConfigurationModel Configuration { get; set; }

        static HashSet<string> IgnorableAudioPaths = new HashSet<string>();
        static HashSet<string> IgnorableIconPaths = new HashSet<string>();

        static string UserFolderPath { get; set; }
        static string LogFileName { get; set; }
        static string LastKnownLocationName { get; set; } // World name
        static string LastKnownLocationID { get; set; } // World ID

        static readonly object logMutex = new object();

        static Timer LogDetectionTimer { get; set; }

        static Dictionary<string, TailSubscription> Subscriptions { get; set; }

        static DateTime SilencedUntil = DateTime.Now,
                        LastMaximumKeywordsNotification = DateTime.Now;

        static XSNotifier Notifier { get; set; }

        static async Task Main(string[] args)
        {
            UserFolderPath = Environment.ExpandEnvironmentVariables(@"%AppData%\..\LocalLow\XSOverlay VRChat Parser");
            if (!Directory.Exists(UserFolderPath))
                Directory.CreateDirectory(UserFolderPath);

            if (!Directory.Exists($@"{UserFolderPath}\Logs"))
                Directory.CreateDirectory($@"{UserFolderPath}\Logs");

            DateTime now = DateTime.Now;
            LogFileName = $"Session_{now.Year:0000}{now.Month:00}{now.Day:00}{now.Hour:00}{now.Minute:00}{now.Second:00}.log";
            Log(LogEventType.Info, $@"Log initialized at {UserFolderPath}\Logs\{LogFileName}");

            try
            {
                if (!File.Exists($@"{UserFolderPath}\config.json"))
                {
                    Configuration = new ConfigurationModel();
                    File.WriteAllText($@"{UserFolderPath}\config.json", Configuration.AsJson());
                }
                else
                    Configuration = JsonSerializer.Deserialize<ConfigurationModel>(File.ReadAllText($@"{UserFolderPath}\config.json"), new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });

                // Rewrite configuration to update it with any new fields not in existing configuration. Useful during update process and making sure the config always has updated annotations.
                // Users shouldn't need to re-configure every time they update the software.
                File.WriteAllText($@"{UserFolderPath}\config.json", Configuration.AsJson());
            }
            catch (Exception ex)
            {
                Log(LogEventType.Error, "An exception occurred while attempting to read or write the configuration file.");
                Log(ex);
                return;
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
            XSGlobals.DefaultVolume = Configuration.NotificationVolume;

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

            await Task.Delay(-1); // Shutdown should be managed by XSO, so just... hang around. Maybe implement periodic checks to see if XSO is running to avoid being orphaned.
        }

        static void Exit()
        {
            Log(LogEventType.Info, "Disposing notifier and exiting application.");

            Notifier.Dispose();

            foreach (var item in Subscriptions)
                item.Value.Dispose();

            Subscriptions.Clear();

            Environment.Exit(-1);
        }

        static void Log(LogEventType type, string message)
        {
            DateTime now = DateTime.Now;
            string dateTimeStamp = $"[{now.Year:0000}/{now.Month:00}/{now.Day:00} {now.Hour:00}:{now.Minute:00}:{now.Second:00}]";

            lock (logMutex)
            {
                switch (type)
                {
                    case LogEventType.Error:
                        File.AppendAllText($@"{UserFolderPath}\Logs\{LogFileName}", $"{dateTimeStamp} [ERROR] {message}\r\n");
                        break;
                    case LogEventType.Event:
                        if (Configuration.LogNotificationEvents)
                            File.AppendAllText($@"{UserFolderPath}\Logs\{LogFileName}", $"{dateTimeStamp} [EVENT] {message}\r\n");
                        break;
                    case LogEventType.Info:
                        File.AppendAllText($@"{UserFolderPath}\Logs\{LogFileName}", $"{dateTimeStamp} [INFO] {message}\r\n");
                        break;
                }
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
                                Icon = IgnorableIconPaths.Contains(Configuration.WorldChangedIconPath) ? Configuration.WorldChangedIconPath : Configuration.GetLocalResourcePath(Configuration.WorldChangedIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.WorldChangedAudioPath) ? Configuration.WorldChangedAudioPath : Configuration.GetLocalResourcePath(Configuration.WorldChangedAudioPath),
                                Title = LastKnownLocationName,
                                Content = $"{(Configuration.DisplayJoinLeaveSilencedOverride ? "" : $"Silencing notifications for {Configuration.WorldJoinSilenceSeconds} seconds.")}",
                                Height = 110
                            }));

                            Log(LogEventType.Event, $"[VRC] World changed to {LastKnownLocationName} -> {LastKnownLocationID}");
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
                                Icon = IgnorableIconPaths.Contains(Configuration.PlayerJoinedIconPath) ? Configuration.PlayerJoinedIconPath : Configuration.GetLocalResourcePath(Configuration.PlayerJoinedIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.PlayerJoinedAudioPath) ? Configuration.PlayerJoinedAudioPath : Configuration.GetLocalResourcePath(Configuration.PlayerJoinedAudioPath),
                                Title = message
                            }));

                            Log(LogEventType.Event, $"[VRC] Join: {message}");
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
                                Icon = IgnorableIconPaths.Contains(Configuration.PlayerLeftIconPath) ? Configuration.PlayerLeftIconPath : Configuration.GetLocalResourcePath(Configuration.PlayerLeftIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.PlayerLeftAudioPath) ? Configuration.PlayerLeftAudioPath : Configuration.GetLocalResourcePath(Configuration.PlayerLeftAudioPath),
                                Title = message
                            }));

                            Log(LogEventType.Event, $"[VRC] Leave: {message}");
                        }
                        // Shader keyword limit exceeded
                        else if (line.Contains("Maximum number (256)"))
                        {
                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.KeywordsExceeded, new XSNotification()
                            {
                                Timeout = Configuration.MaximumKeywordsExceededTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.MaximumKeywordsExceededIconPath) ? Configuration.MaximumKeywordsExceededIconPath : Configuration.GetLocalResourcePath(Configuration.MaximumKeywordsExceededIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.MaximumKeywordsExceededAudioPath) ? Configuration.MaximumKeywordsExceededAudioPath : Configuration.GetLocalResourcePath(Configuration.MaximumKeywordsExceededAudioPath),
                                Title = "Maximum shader keywords exceeded!"
                            }));

                            Log(LogEventType.Event, $"[VRC] Maximum shader keywords exceeded!");
                        }
                        // Portal dropped
                        else if (line.Contains("[Behaviour]") && line.Contains("Portals/PortalInternalDynamic"))
                        {
                            ToSend.Add(new Tuple<EventType, XSNotification>(EventType.PortalDropped, new XSNotification()
                            {
                                Timeout = Configuration.PortalDroppedTimeoutSeconds,
                                Icon = IgnorableIconPaths.Contains(Configuration.PortalDroppedIconPath) ? Configuration.PortalDroppedIconPath : Configuration.GetLocalResourcePath(Configuration.PortalDroppedIconPath),
                                AudioPath = IgnorableAudioPaths.Contains(Configuration.PortalDroppedAudioPath) ? Configuration.PortalDroppedAudioPath : Configuration.GetLocalResourcePath(Configuration.PortalDroppedAudioPath),
                                Title = "A portal has been spawned."
                            }));

                            Log(LogEventType.Event, $"[VRC] Portal dropped.");
                        }
                    }
                }
            }

            if (ToSend.Count > 0)
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
