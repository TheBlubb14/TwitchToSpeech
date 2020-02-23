using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Data;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchToSpeech.Model;
using TwitchToSpeech.View;

namespace TwitchToSpeech.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public ICommand MenuItemExitCommand { get; set; }
        public ICommand MenuItemSettingsCommand { get; set; }
        public ICommand LoadedCommand { get; set; }
        public ISnackbarMessageQueue SnackbarMessageQueue { get; set; }
        public bool IsTwitchConnected { get; set; }
        public ICommand ConnectToTwitchCommand { get; set; }
        public string PrefixList { get; set; }
        public ICommand SavePrefixListCommand { get; set; }
        public ICommand KeyDownCommand { get; set; }

        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();

        private readonly ObservableCollection<Exception> StartupExceptions = new ObservableCollection<Exception>();
        private bool IsSettingsDialogOpen;
        private SpeechSynthesizer speech;
        private TwitchAPI twitchAPI;
        private TwitchClient twitchClient;
        private string[] prefixList;

        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
            }
            else
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs\\log.txt")
                    .WriteTo.ObservableCollection(Logs, Dispatcher.CurrentDispatcher)
                    .CreateLogger();

                // Code runs "for real"
                this.PropertyChanged += this.MainViewModel_PropertyChanged;
                LoadedCommand = new RelayCommand(Loaded);
                KeyDownCommand = new RelayCommand<KeyEventArgs>(KeyDown);
                MenuItemSettingsCommand = new RelayCommand(OpenSettings);
                MenuItemExitCommand = new RelayCommand(Application.Current.Shutdown);
                ConnectToTwitchCommand = new RelayCommand(ConnectToTwitch);
                SavePrefixListCommand = new RelayCommand(SavePrefixList);

                speech = new SpeechSynthesizer();
                // TODO: speech.SelectVoice();
            }
        }

        private void Loaded()
        {
            try
            {
                prefixList = Settings.Instance.PrefixList ?? new string[0];
                PrefixList = string.Join(Environment.NewLine, prefixList);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        public void ConnectToTwitch()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Settings.Instance.Username))
                {
                    ShowMessage("Twitch Benutzername ist in den Einstellungen nicht gesetzt");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Settings.Instance.OAuthToken))
                {
                    ShowMessage("Twitch O Auth Token ist in den Einstellungen nicht gesetzt");
                    return;
                }

                var loggerfactory = new LoggerFactory()
                    .AddSerilog(
                    new LoggerConfiguration()
                    .WriteTo.File("logs\\twitch.log")
                    .CreateLogger());

                twitchAPI = new TwitchAPI(loggerfactory);
                twitchClient = new TwitchClient(logger: loggerfactory.CreateLogger<TwitchClient>());
                twitchClient.OnConnected += TwitchClient_OnConnected;
                twitchClient.OnNewSubscriber += TwitchClient_OnNewSubscriber;
                twitchClient.OnRaidNotification += TwitchClient_OnRaidNotification;
                twitchClient.OnUserJoined += TwitchClient_OnUserJoined;
                twitchClient.OnUserLeft += TwitchClient_OnUserLeft;
                twitchClient.OnBeingHosted += TwitchClient_OnBeingHosted;
                twitchClient.OnMessageReceived += TwitchClient_OnMessageReceived;

                twitchClient.Initialize(new ConnectionCredentials(Settings.Instance.Username, Settings.Instance.OAuthToken), Settings.Instance.ChannelToJoin);
                twitchClient.Connect();

                IsTwitchConnected = true;
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void TwitchClient_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (!Settings.Instance.MessageNotification)
                return;

            if (e.ChatMessage.IsMe)
                return;

            if (prefixList?.Any(x => e.ChatMessage.Message.StartsWith(x, StringComparison.OrdinalIgnoreCase)) ?? false)
                return;

            ShowMessage($"{e.ChatMessage.Username}: {e.ChatMessage.Message}");
        }

        private void TwitchClient_OnBeingHosted(object sender, OnBeingHostedArgs e)
        {
            if (!Settings.Instance.BeingHostedNotification)
                return;

            if (e.BeingHostedNotification.IsAutoHosted)
                return;

            ShowMessage($"{e.BeingHostedNotification.HostedByChannel} hosted mit {e.BeingHostedNotification.Viewers} Leuten");
        }

        private void TwitchClient_OnUserLeft(object sender, OnUserLeftArgs e)
        {
            if (!Settings.Instance.UserLeftNotification)
                return;

            ShowMessage($"{e.Username} ist weg");
        }

        private void TwitchClient_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            if (!Settings.Instance.UserJoinedNotification)
                return;

            ShowMessage($"{e.Username} ist da");
        }

        private void TwitchClient_OnRaidNotification(object sender, OnRaidNotificationArgs e)
        {
            if (!Settings.Instance.RaidNotification)
                return;

            ShowMessage($"{e.RaidNotification.DisplayName} raidet mit {e.RaidNotification.MsgParamViewerCount} Leuten");
        }

        private void TwitchClient_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            if (!Settings.Instance.SubscriberNotification)
                return;

            ShowMessage($"{e.Subscriber.DisplayName} hat abonniert");
        }

        private void TwitchClient_OnConnected(object sender, OnConnectedArgs e)
        {
            ShowMessage("Kleint verbunden");
        }

        private void KeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.E && IsControl)
                MenuItemExitCommand.Execute(null);
            else if (e.Key == Key.X && IsControl)
                MenuItemSettingsCommand.Execute(null);
        }

        private bool IsControl => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        private void ShowMessage(string text)
        {
            Log.Information(text);
            Speak(text);
        }

        private void MainViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SnackbarMessageQueue):
                    ShowStartupExceptions();
                    break;
                case nameof(PrefixList):
                    prefixList = PrefixList.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    break;
            }
        }

        private void ShowError(Exception exception, [CallerMemberName]string caller = null)
        {
            Log.Error(exception, caller ?? "");
            if (SnackbarMessageQueue != null)
            {
                SnackbarMessageQueue.Enqueue($"error: {exception.Message}", "show more",
                    () => SnackbarMessageQueue.Enqueue($"{exception}"));
            }
            else
            {
                // if theres an exception at application startup, 
                // the viewmodel loads before the view and SnackbarMessageQueue is not declared yet
                StartupExceptions.Add(exception);
            }
        }

        private void ShowStartupExceptions()
        {
            foreach (var ex in StartupExceptions)
                ShowError(ex);

            StartupExceptions.Clear();
        }

        public void Speak(string text)
        {
            speech.SpeakAsync(text);
        }

        private async void OpenSettings()
        {
            if (!IsSettingsDialogOpen)
            {
                var model = SimpleIoc.Default.GetInstance<SettingsViewModel>();
                var settings = new SettingsControl() { DataContext = model };
                await DialogHost.Show(settings,
                    new DialogOpenedEventHandler((obj, args) => IsSettingsDialogOpen = true),
                    new DialogClosingEventHandler((obj, args) =>
                    {
                        IsSettingsDialogOpen = false;

                        if (args.Parameter.ToString() == "1")
                        {
                            Settings.Instance = model.Settings;
                            Settings.Safe();
                        }
                    })).ConfigureAwait(false);
            }
        }

        private void SavePrefixList()
        {
            Settings.Instance.PrefixList = prefixList;
            Settings.Safe();
        }
    }
}