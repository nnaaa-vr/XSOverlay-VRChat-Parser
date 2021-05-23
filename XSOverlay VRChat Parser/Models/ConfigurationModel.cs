using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XSNotifications.Enum;
using XSNotifications.Helpers;
using XSOverlay_VRChat_Parser.Helpers;

namespace XSOverlay_VRChat_Parser.Models
{
    public class ConfigurationModel
    {
        public const string UserFolderPath = @"%AppData%\..\LocalLow\XSOverlay VRChat Parser";
        public static string ExpandedUserFolderPath = Environment.ExpandEnvironmentVariables(UserFolderPath);
        private static object ConfigMutex = new object();

        [Annotation("Polling frequency for individual log file updates.", true, "GENERAL CONFIGURATION")]
        public long ParseFrequencyMilliseconds { get; set; }
        [Annotation("Polling frequency for new logs in OutputLogRoot")]
        public long DirectoryPollFrequencyMilliseconds { get; set; }
        [Annotation("Absolute path to output log root for VRChat. Environment variables will be expanded.")]
        public string OutputLogRoot { get; set; }
        [Annotation("Determines whether or not logs of parsed events will be written to the session log in the user folder. Valid values: true, false")]
        public bool LogNotificationEvents { get; set; }
        [Annotation("Opacity for toast notifications. Valid values: 0.0 -> 1.0.")]
        public float Opacity { get; set; }

        [Annotation("Determines whether or not player join notifications are delivered. Valid values: true, false", true, "PLAYER JOINED")]
        public bool DisplayPlayerJoined { get; set; }
        [Annotation("Period of time in seconds for the player join notification to remain on screen. Valid values: 0.0 -> float32 max")]
        public float PlayerJoinedNotificationTimeoutSeconds { get; set; }
        [Annotation("Volume for incoming notification sounds. Valid values: 0.0 -> 1.0.")]
        public float PlayerJoinedNotificationVolume { get; set; }
        [Annotation("Relative path to icon for player joins. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string PlayerJoinedIconPath { get; set; }
        [Annotation("Relative path to ogg-formatted audio for player joins. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string PlayerJoinedAudioPath { get; set; }

        [Annotation("Determines whether or not player left notifications are delivered. Valid values: true, false", true, "PLAYER LEFT")]
        public bool DisplayPlayerLeft { get; set; }
        [Annotation("Period of time in seconds for the player left notification to remain on screen. Valid values: 0.0 -> float32 max")]
        public float PlayerLeftNotificationTimeoutSeconds { get; set; }
        [Annotation("Volume for incoming notification sounds. Valid values: 0.0 -> 1.0.")]
        public float PlayerLeftNotificationVolume { get; set; }
        [Annotation("Relative path to icon for player left. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string PlayerLeftIconPath { get; set; }
        [Annotation("Relative path to ogg-formatted audio for player left. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string PlayerLeftAudioPath { get; set; }

        [Annotation("Determines whether or not world change notifications are delivered. Valid values: true, false", true, "WORLD CHANGED")]
        public bool DisplayWorldChanged { get; set; }
        [Annotation("Period of time in seconds for player join/leave notifications to be silenced on world join. This is to avoid spam from enumerating everyone currently in the target world. Valid values: 0.0 -> float32 max")]
        public long WorldJoinSilenceSeconds { get; set; }
        [Annotation("Determines whether or not player join/leave notifications are silenced on world join. Warning, this gets spammy if on! Valid values: true, false")]
        public bool DisplayJoinLeaveSilencedOverride { get; set; }
        [Annotation("Period of time in seconds for the world changed notification to remain on screen. Value values: 0.0 -> float32 max")]
        public float WorldChangedNotificationTimeoutSeconds { get; set; }
        [Annotation("Volume for incoming notification sounds. Valid values: 0.0 -> 1.0.")]
        public float WorldChangedNotificationVolume { get; set; }
        [Annotation("Relative path to icon for world changed. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string WorldChangedIconPath { get; set; }
        [Annotation("Relative path to ogg-formatted audio for world changed. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string WorldChangedAudioPath { get; set; }

        [Annotation("Determines whether or not shader keywords exceeded notifications are delivered. Valid values: true, false", true, "SHADER KEYWORDS EXCEEDED")]
        public bool DisplayMaximumKeywordsExceeded { get; set; }
        [Annotation("Period of time in seconds for shader keywords exceeded notification to remain on screen. Valid values: 0.0 -> float32 max")]
        public float MaximumKeywordsExceededTimeoutSeconds { get; set; }
        [Annotation("Period of time in seconds after a shader keywords exceeded notification is sent to ignore shader keywords exceeded events. Valid values: 0.0 -> float32 max")]
        public float MaximumKeywordsExceededCooldownSeconds { get; set; }
        [Annotation("Volume for incoming notification sounds. Valid values: 0.0 -> 1.0.")]
        public float MaximumKeywordsExceededNotificationVolume { get; set; }
        [Annotation("Relative path to icon for shader keywords exceeded. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string MaximumKeywordsExceededIconPath { get; set; }
        [Annotation("Relative path to ogg-formatted audio for shader keywords exceeded. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string MaximumKeywordsExceededAudioPath { get; set; }

        [Annotation("Determines whether or not portal dropped notifications are delivered. Valid values: true, false", true, "PORTAL DROPPED")]
        public bool DisplayPortalDropped { get; set; }
        [Annotation("Period of time in seconds for portal dropped notification to remain on screen. Valid values: 0.0 -> float32 max")]
        public float PortalDroppedTimeoutSeconds { get; set; }
        [Annotation("Volume for incoming notification sounds. Valid values: 0.0 -> 1.0.")]
        public float PortalDroppedNotificationVolume { get; set; }
        [Annotation("Relative path to icon for portal dropped. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string PortalDroppedIconPath { get; set; }
        [Annotation("Relative path to ogg-formatted audio for portal dropped. Other valid values include: \"\", \"default\", \"warning\", \"error\"")]
        public string PortalDroppedAudioPath { get; set; }

        public ConfigurationModel()
        {
            ParseFrequencyMilliseconds = 300;
            DirectoryPollFrequencyMilliseconds = 5000;
            OutputLogRoot = @"%AppData%\..\LocalLow\VRChat\vrchat";
            LogNotificationEvents = true;
            Opacity = 0.75f;

            DisplayPlayerJoined = true;
            PlayerJoinedNotificationTimeoutSeconds = 2.5f;
            PlayerJoinedNotificationVolume = 0.2f;
            PlayerJoinedIconPath = @"\Resources\Icons\player_joined.png";
            PlayerJoinedAudioPath = @"\Resources\Audio\player_joined.ogg";

            DisplayPlayerLeft = true;
            PlayerLeftNotificationTimeoutSeconds = 2.5f;
            PlayerLeftNotificationVolume = 0.2f;
            PlayerLeftIconPath = @"\Resources\Icons\player_left.png";
            PlayerLeftAudioPath = @"\Resources\Audio\player_left.ogg";

            DisplayWorldChanged = true;
            WorldJoinSilenceSeconds = 20;
            DisplayJoinLeaveSilencedOverride = true;
            WorldChangedNotificationTimeoutSeconds = 3.0f;
            WorldChangedNotificationVolume = 0.2f;
            WorldChangedIconPath = @"\Resources\Icons\world_changed.png";
            WorldChangedAudioPath = XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Default);

            DisplayMaximumKeywordsExceeded = false;
            MaximumKeywordsExceededTimeoutSeconds = 3.0f;
            MaximumKeywordsExceededCooldownSeconds = 600.0f;
            MaximumKeywordsExceededNotificationVolume = 0.2f;
            MaximumKeywordsExceededIconPath = @"\Resources\Icons\keywords_exceeded.png";
            MaximumKeywordsExceededAudioPath = XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Warning);

            DisplayPortalDropped = true;
            PortalDroppedTimeoutSeconds = 3.0f;
            PortalDroppedNotificationVolume = 0.2f;
            PortalDroppedIconPath = @"\Resources\Icons\portal_dropped.png";
            PortalDroppedAudioPath = XSGlobals.GetBuiltInAudioSourceString(XSAudioDefault.Default);
        }

        public string GetLocalResourcePath(string path) => Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + path;

        public static void Save(ConfigurationModel model)
        {
            lock (ConfigMutex)
            {
                if (!Directory.Exists(ExpandedUserFolderPath))
                    Directory.CreateDirectory(ExpandedUserFolderPath);
                if (!Directory.Exists($@"{ExpandedUserFolderPath}\Logs"))
                    Directory.CreateDirectory($@"{ExpandedUserFolderPath}\Logs");

                File.WriteAllText($@"{ExpandedUserFolderPath}\config.json", model.AsJson());
            }
        }

        public static ConfigurationModel Load()
        {
            lock (ConfigMutex)
            {
                if (!File.Exists($@"{ExpandedUserFolderPath}\config.json"))
                    File.WriteAllText($@"{ExpandedUserFolderPath}\config.json", new ConfigurationModel().AsJson());

                return JsonSerializer.Deserialize<ConfigurationModel>(File.ReadAllText($@"{ExpandedUserFolderPath}\config.json"),
                    new JsonSerializerOptions() { ReadCommentHandling = JsonCommentHandling.Skip });
            }
        }

        public string AsJson(bool annotated = true)
        {
            string asJson = JsonSerializer.Serialize<ConfigurationModel>(this, new JsonSerializerOptions() { WriteIndented = true });

            // JSON teeeeeeeeechnically doesn't support comments, but we can add them and there's a flag in our json reader for skipping them, so we're good. Theoretically.
            if (annotated)
            {
                string[] lines = asJson.Split('\n');

                StringBuilder sb = new StringBuilder();

                PropertyInfo[] properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                Dictionary<string, Tuple<PropertyInfo, Annotation>> propertyAnnotations = new Dictionary<string, Tuple<PropertyInfo, Annotation>>();

                foreach (PropertyInfo p in properties)
                    propertyAnnotations.Add(p.Name.ToLower(), new Tuple<PropertyInfo, Annotation>(p,
                        (Annotation)p.GetCustomAttributes(typeof(Annotation), true).GetValue(0)));

                StringBuilder commentBuilder = new StringBuilder();
                StringBuilder propertyBuilder = new StringBuilder();
                foreach (string line in lines)
                {
                    if (line.Trim() == "}")
                    {
                        if (propertyBuilder.ToString() != string.Empty)
                        {
                            if (commentBuilder.Length > 0)
                                sb.Append('\n' + commentBuilder.ToString() + '\n');
                            sb.Append(propertyBuilder.ToString() + "}");
                            continue;
                        }
                    }
                    else if (!line.Contains(':'))
                    {
                        sb.Append(line + '\n');
                        continue;
                    }

                    string propertyNameLower = line.Substring(0, line.IndexOf(':')).Trim().Replace("\"", "").ToLower();
                    string whitespace = line.Substring(0, line.IndexOf('\"'));

                    if (propertyAnnotations.ContainsKey(propertyNameLower))
                    {
                        Tuple<PropertyInfo, Annotation> pa = propertyAnnotations[propertyNameLower];

                        if (pa.Item2.startsGroup)
                        {
                            if (commentBuilder.Length > 0)
                                sb.Append('\n' + commentBuilder.ToString() + '\n');
                            if (propertyBuilder.Length > 0)
                                sb.Append(propertyBuilder.ToString());

                            commentBuilder.Clear();
                            propertyBuilder.Clear();

                            commentBuilder.Append($"{whitespace}// {pa.Item2.groupDescription}\n");
                        }

                        commentBuilder.Append($"{whitespace}// {pa.Item1.Name} : {pa.Item2.description}\n");
                        propertyBuilder.Append(line + '\n');
                    }
                    else
                        propertyBuilder.Append(line + '\n');
                }

                asJson = sb.ToString();
            }

            return asJson;
        }
    }
}
