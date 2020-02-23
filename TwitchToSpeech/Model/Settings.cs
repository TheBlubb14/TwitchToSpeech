using GalaSoft.MvvmLight;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace TwitchToSpeech.Model
{
    public class Settings : ObservableObject
    {
        public static Settings Instance = new Settings();
        public static readonly string Location;

        static Settings()
        {
            var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Assembly.GetExecutingAssembly().GetName().Name);

            if (!Directory.Exists(applicationPath))
                Directory.CreateDirectory(applicationPath);

            Location = Path.Combine(applicationPath, "settings.json");

            Load();
        }

        public static void Safe()
        {
            File.WriteAllText(Location, JsonConvert.SerializeObject(Instance, Formatting.Indented));
        }

        public static void Load()
        {
            if (File.Exists(Location))
                Instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Location));
        }

        public string Username { get; set; }

        public string OAuthToken { get; set; }

        public string ChannelToJoin { get; set; }

        public string[] PrefixList { get; set; }

        public bool SubscriberNotification { get; set; }

        public bool RaidNotification { get; set; }

        public bool UserJoinedNotification { get; set; }

        public bool UserLeftNotification { get; set; }

        public bool BeingHostedNotification { get; set; }

        public bool MessageNotification { get; set; }
    }
}
