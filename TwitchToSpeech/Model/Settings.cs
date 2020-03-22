using GalaSoft.MvvmLight;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TwitchLib.Communication.Models;

namespace TwitchToSpeech.Model
{
    public class Settings : ObservableObject
    {
        public static readonly string Location;

        static Settings()
        {
            var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Assembly.GetExecutingAssembly().GetName().Name);

            if (!Directory.Exists(applicationPath))
                Directory.CreateDirectory(applicationPath);

            Location = Path.Combine(applicationPath, "settings.json");
        }

        public static void Safe(Settings settings)
        {
            File.WriteAllText(Location, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        public static Settings Load()
        {
            var settings = File.Exists(Location)
                ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Location))
                : new Settings();

            if (settings.SubscriberNotification is null)
                settings.SubscriberNotification = new NotificationSetting();

            if (settings.RaidNotification is null)
                settings.RaidNotification = new NotificationSetting();

            if (settings.UserJoinedNotification is null)
                settings.UserJoinedNotification = new NotificationSetting();

            if (settings.UserLeftNotification is null)
                settings.UserLeftNotification = new NotificationSetting();

            if (settings.BeingHostedNotification is null)
                settings.BeingHostedNotification = new NotificationSetting();

            if (settings.MessageNotification is null)
                settings.MessageNotification = new NotificationSetting();

            if (settings.ClientConnectedNotification is null)
                settings.ClientConnectedNotification = new NotificationSetting();

            if (settings.NewFollowerNotification is null)
                settings.NewFollowerNotification = new NotificationSetting();

            if (settings.BabelSettings is null)
            {
                settings.BabelSettings = new BabelSettings()
                {
                    Languages = new ObservableCollection<BabelLanguage>(
                        BabelSettings.SupportedLanguages.Select(x => new BabelLanguage(x)))
                };
            }

            return settings;
        }

        public Settings()
        {
            PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PrefixListText):
                    PrefixList = PrefixListText.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    break;

                case nameof(UserBlacklistText):
                    UserBlacklist = UserBlacklistText.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    break;

                case nameof(UserNicknamesText):
                    UserNicknames =
                        new Dictionary<string, string>(
                            UserNicknamesText
                            .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries))
                            .ToDictionary(x => x[0], y => y[1]),
                       StringComparer.OrdinalIgnoreCase);
                    break;

                // For every class we have to call this pattern
                case nameof(SubscriberNotification):
                    SubscriberNotification.PropertyChanged += (s, e) => Safe(this);
                    SubscriberNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(RaidNotification):
                    RaidNotification.PropertyChanged += (s, e) => Safe(this);
                    RaidNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(UserJoinedNotification):
                    UserJoinedNotification.PropertyChanged += (s, e) => Safe(this);
                    UserJoinedNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(UserLeftNotification):
                    UserLeftNotification.PropertyChanged += (s, e) => Safe(this);
                    UserLeftNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(BeingHostedNotification):
                    BeingHostedNotification.PropertyChanged += (s, e) => Safe(this);
                    BeingHostedNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(MessageNotification):
                    MessageNotification.PropertyChanged += (s, e) => Safe(this);
                    MessageNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(ClientConnectedNotification):
                    ClientConnectedNotification.PropertyChanged += (s, e) => Safe(this);
                    ClientConnectedNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(NewFollowerNotification):
                    NewFollowerNotification.PropertyChanged += (s, e) => Safe(this);
                    NewFollowerNotification.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;

                case nameof(BabelSettings):
                    BabelSettings.PropertyChanged += (s, e) => Safe(this);
                    BabelSettings.PropertyChanged += (s, e) => PropertyChangedHandler.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
                    break;
            }

            Safe(this);
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

        public NotificationSetting NewFollowerNotification { get; set; }

        public bool ConnectToPipeServer { get; set; }

        public string PipeServerName { get; set; }

        public bool CheckForNewFollowers { get; set; }

        public string ClientId { get; set; }

        public string AccessToken { get; set; }

        public string UserBlacklistText { get; set; }

        [JsonIgnore]
        public IReadOnlyCollection<string> UserBlacklist { get; set; }

        public string UserNicknamesText { get; set; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> UserNicknames { get; set; }

        public bool ReplaceBsr { get; set; }

        public BabelSettings BabelSettings { get; set; }

        public bool ScrollChatToEnd { get; set; }

        public bool LogBabelResult { get; set; }
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

    public class BabelSettings : ObservableObject
    {
        public bool DynamicallySwitchLanguage { get; set; }

        public ObservableCollection<BabelLanguage> Languages { get; set; }

        [JsonIgnore]
        public static readonly string[] SupportedLanguages = new string[]
        {
            "nl", "en", "ca", "fr", "es", "no", "da", "it", "sv",
            "de", "pt", "ro", "vi", "tr", "fi", "hu", "cs", "pl",
            "el", "fa", "he", "sr", "sl", "ar", "nn", "ru", "et",
            "ko", "hi", "is", "th", "bn", "ja", "zh", "se"
        };
    }

    public class BabelLanguage : ObservableObject
    {
        public CultureInfo Culture { get; }

        public string Code => Culture.IetfLanguageTag;

        public bool Selected { get; set; }

        public BabelLanguage(string code)
        {
            Culture = CultureInfo.GetCultureInfo(code);
        }
    }
}
