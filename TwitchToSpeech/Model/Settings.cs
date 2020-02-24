using GalaSoft.MvvmLight;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            {
                Instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Location));

                if (Instance.SubscriberNotification is null)
                    Instance.SubscriberNotification = new NotificationSetting();

                if (Instance.RaidNotification is null)
                    Instance.RaidNotification = new NotificationSetting();

                if (Instance.UserJoinedNotification is null)
                    Instance.UserJoinedNotification = new NotificationSetting();

                if (Instance.UserLeftNotification is null)
                    Instance.UserLeftNotification = new NotificationSetting();

                if (Instance.BeingHostedNotification is null)
                    Instance.BeingHostedNotification = new NotificationSetting();

                if (Instance.MessageNotification is null)
                    Instance.MessageNotification = new NotificationSetting();

                if (Instance.ClientConnectedNotification is null)
                    Instance.ClientConnectedNotification = new NotificationSetting();
            }
        }

        public Settings()
        {
            this.PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PrefixListText))
                PrefixList = PrefixListText.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }

        [JsonIgnore]
        public IReadOnlyCollection<string> PrefixList { get; set; }

        public string Username { get; set; }

        public string OAuthToken { get; set; }

        public string ChannelToJoin { get; set; }

        public string PrefixListText { get; set; }

        public NotificationSetting SubscriberNotification { get; set; }

        public NotificationSetting RaidNotification { get; set; }

        public NotificationSetting UserJoinedNotification { get; set; }

        public NotificationSetting UserLeftNotification { get; set; }

        public NotificationSetting BeingHostedNotification { get; set; }

        public NotificationSetting MessageNotification { get; set; }

        public NotificationSetting ClientConnectedNotification { get; set; }

        public bool ConnectToPipeServer { get; set; }

        public string PipeServerName { get; set; }
    }

    public class NotificationSetting : ObservableObject
    {
        public bool Speech { get; set; }
        public bool Text { get; set; }

        public NotificationSetting()
        {

        }

        public NotificationSetting(bool speech, bool text)
        {
            this.Speech = speech;
            this.Text = text;
        }
    }
}
