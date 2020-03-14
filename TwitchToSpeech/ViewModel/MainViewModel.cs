using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TwitchLib.Api;
using TwitchLib.Api.Services;
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
        public ICommand ClosedCommand { get; set; }
        public ISnackbarMessageQueue SnackbarMessageQueue { get; set; }
        public bool IsTwitchConnected { get; set; }
        public ICommand ConnectToTwitchCommand { get; set; }
        public ICommand KeyDownCommand { get; set; }

        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();

        private readonly ObservableCollection<Exception> StartupExceptions = new ObservableCollection<Exception>();
        private bool IsSettingsDialogOpen;
        private SpeechSynthesizer speech;
        private TwitchAPI twitchAPI;
        private TwitchClient twitchClient;
        private NamedPipeClientStream pipeClient;
        private CancellationTokenSource pipeClientCTS;
        private ConcurrentQueue<string> pipeMessages = new ConcurrentQueue<string>();
        private FollowerService followerService;

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
                ClosedCommand = new RelayCommand(Closed);
                KeyDownCommand = new RelayCommand<KeyEventArgs>(KeyDown);
                MenuItemSettingsCommand = new RelayCommand(OpenSettings);
                MenuItemExitCommand = new RelayCommand(Application.Current.Shutdown);
                ConnectToTwitchCommand = new RelayCommand(ConnectToTwitch);

                speech = new SpeechSynthesizer();
                // TODO: speech.SelectVoice();
            }
        }

        private void Loaded()
        {
            try
            {
                SettingsChanged();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void Closed()
        {
            try
            {
                DisposePipeClient();
                followerService?.Stop();
                twitchClient?.Disconnect();
            }
            catch (Exception ex)
            {
                // Doesnt make sense to show an error if all is already closed
                //ShowError(ex);
            }
        }

        public void ConnectToTwitch()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Settings.Instance.Username))
                {
                    ShowMessage("Twitch Benutzername ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                    return;
                }

                if (string.IsNullOrWhiteSpace(Settings.Instance.OAuthToken))
                {
                    ShowMessage("Twitch O Auth Token ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                    return;
                }

                if (Settings.Instance.CheckForNewFollowers)
                {
                    if (string.IsNullOrWhiteSpace(Settings.Instance.ClientId))
                    {
                        ShowMessage("Twitch Api Client ID ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Settings.Instance.AccessToken))
                    {
                        ShowMessage("Twitch Api Access Token ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                        return;
                    }
                }

                var loggerfactory = new LoggerFactory()
                .AddSerilog(
                new LoggerConfiguration()
                .WriteTo.File("logs\\twitch.log")
                .CreateLogger());

                twitchAPI = new TwitchAPI(loggerfactory);
                twitchAPI.Settings.ClientId = Settings.Instance.ClientId;
                twitchAPI.Settings.AccessToken = Settings.Instance.AccessToken;
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

                if (Settings.Instance.CheckForNewFollowers)
                {
                    followerService = new FollowerService(twitchAPI, checkIntervalInSeconds: 5);
                    followerService.SetChannelsByName(new List<string>() { Settings.Instance.ChannelToJoin });
                    followerService.OnNewFollowersDetected += FollowerService_OnNewFollowersDetected;
                    followerService.UpdateLatestFollowersAsync(false);
                    followerService.Start();
                }

                IsTwitchConnected = true;
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void FollowerService_OnNewFollowersDetected(object sender, TwitchLib.Api.Services.Events.FollowerService.OnNewFollowersDetectedArgs e)
        {
            var followers = e.NewFollowers.Select(x => x.FromUserName);
            var msg = string.Join(" und ", followers);
            var msgEndIndex = msg.LastIndexOf(" und ");

            if (msgEndIndex > 0)
                msg = msg.Substring(0, msgEndIndex);

            ShowMessage($"{msg} {(followers.Count() > 1 ? "folgen" : "folgt")} nun", Settings.Instance.NewFollowerNotification);
        }

        #region Twitch Events
        private void TwitchClient_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.IsMe)
                return;

            if (Settings.Instance.PrefixList?.Any(x => e.ChatMessage.Message.StartsWith(x, StringComparison.OrdinalIgnoreCase)) ?? false)
                return;

            ShowMessage($"{e.ChatMessage.Username}: {e.ChatMessage.Message}", Settings.Instance.MessageNotification);
        }

        private void TwitchClient_OnBeingHosted(object sender, OnBeingHostedArgs e)
        {
            if (e.BeingHostedNotification.IsAutoHosted)
                return;

            ShowMessage($"{e.BeingHostedNotification.HostedByChannel} hosted mit {e.BeingHostedNotification.Viewers} Leuten", Settings.Instance.BeingHostedNotification);
        }

        private void TwitchClient_OnUserLeft(object sender, OnUserLeftArgs e)
        {
            ShowMessage($"{e.Username} ist weg", Settings.Instance.UserLeftNotification);
        }

        private void TwitchClient_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            ShowMessage($"{e.Username} ist da", Settings.Instance.UserJoinedNotification);
        }

        private void TwitchClient_OnRaidNotification(object sender, OnRaidNotificationArgs e)
        {
            ShowMessage($"{e.RaidNotification.DisplayName} raidet mit {e.RaidNotification.MsgParamViewerCount} Leuten", Settings.Instance.RaidNotification);
        }

        private void TwitchClient_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            ShowMessage($"{e.Subscriber.DisplayName} hat abonniert", Settings.Instance.SubscriberNotification);
        }

        private void TwitchClient_OnConnected(object sender, OnConnectedArgs e)
        {
            ShowMessage("Kleint verbunden", Settings.Instance.ClientConnectedNotification);
        }
        #endregion

        private void KeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.E && IsControl)
                MenuItemExitCommand.Execute(null);
            else if (e.Key == Key.X && IsControl)
                MenuItemSettingsCommand.Execute(null);
        }

        private bool IsControl => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        private void ShowMessage(string text, NotificationSetting userLeftNotification)
        {
            if (userLeftNotification.Text)
                Log.Information(text);
            if (userLeftNotification.Speech)
                Speak(text);
            if (Settings.Instance.ConnectToPipeServer)
                pipeMessages.Enqueue(text);
        }

        private void SettingsChanged()
        {
            try
            {
                if (Settings.Instance.ConnectToPipeServer)
                    Task.Run(StartPipeClient);
                else
                    DisposePipeClient();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        #region Pipe Client
        private async Task StartPipeClient()
        {
            try
            {
                // Already started
                if (pipeClient != null)
                    return;

                pipeClientCTS = new CancellationTokenSource();
                pipeClient = new NamedPipeClientStream(".", Settings.Instance.PipeServerName, PipeDirection.Out);
                await pipeClient.ConnectAsync(pipeClientCTS.Token).ConfigureAwait(false);

                while (pipeClientCTS != null && !pipeClientCTS.IsCancellationRequested)
                {
                    if (pipeMessages.TryDequeue(out var msg))
                    {
                        var buffer = Encoding.UTF32.GetBytes(msg);
                        pipeClient.Write(buffer, 0, buffer.Length);
                        pipeClient.Flush();
                    }
                    else
                    {
                        // Avoid heavy CPU load
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DisposePipeClient()
        {
            pipeClientCTS?.Cancel();
            pipeClientCTS?.Dispose();
            pipeClientCTS = null;

            pipeClient?.Dispose();
            pipeClientCTS = null;
        }
        #endregion

        private void MainViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SnackbarMessageQueue):
                    ShowStartupExceptions();
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
                            SettingsChanged();
                        }
                    })).ConfigureAwait(false);
            }
        }
    }
}