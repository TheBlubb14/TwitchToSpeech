using BeatSaverSharp;
using CommonServiceLocator;
using DialogueMaster.Babel;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using TwitchLib.Api;
using TwitchLib.Api.Services;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchToSpeech.Model;
using TwitchToSpeech.View;
using Windows.Media.SpeechSynthesis;

namespace TwitchToSpeech.ViewModel
{
    public class CockpitViewModel : ViewModelBase, IDisposable
    {
        public bool IsTwitchConnected { get; set; }
        public ICommand ConnectToTwitchCommand { get; set; }
        public Settings Settings => ServiceLocator.Current.GetInstance<Settings>();
        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();

        private MainViewModel mainViewModel => ServiceLocator.Current.GetInstance<MainViewModel>();

        private readonly Regex bsrRegex = new Regex(@"!bsr\s([^\s]*)");
        private readonly Regex babelRegex = new Regex(@"\p{L}");
        private readonly SpeechSynthesizer speech;
        private readonly TaskQueue taskQueue;
        private readonly Dispatcher dispatcher;
        private TwitchAPI twitchAPI;
        private TwitchClient twitchClient;
        private NamedPipeClientStream pipeClient;
        private CancellationTokenSource pipeClientCTS;
        private readonly ConcurrentQueue<string> pipeMessages = new ConcurrentQueue<string>();
        private FollowerService followerService;
        private BabelModel babelModel;
        public readonly Serilog.ILogger logger;
        private readonly BeatSaver beatSaver;

        public CockpitViewModel()
        {
            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
            }
            else
            {
                logger = new LoggerConfiguration()
                    .WriteTo.Logger(Log.Logger)
                    .WriteTo.ObservableCollection(Logs, Dispatcher.CurrentDispatcher)
                    .CreateLogger();

                beatSaver = new BeatSaver(new HttpOptions()
                {
                    ApplicationName = "TwitchToSpeech",
                    Version = Assembly.GetExecutingAssembly().GetName().Version,
                    HandleRateLimits = true
                });

                ConnectToTwitchCommand = new RelayCommand(ConnectToTwitch);
                dispatcher = Dispatcher.CurrentDispatcher;
                taskQueue = new TaskQueue();
                speech = new SpeechSynthesizer();

                Settings.PropertyChanged += (s, e) => SettingsChanged();
                SettingsChanged();
            }
        }

        public void ConnectToTwitch()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Settings.Username))
                {
                    ShowMessage("Twitch Benutzername ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                    return;
                }

                if (string.IsNullOrWhiteSpace(Settings.OAuthToken))
                {
                    ShowMessage("Twitch O Auth Token ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                    return;
                }

                if (Settings.CheckForNewFollowers)
                {
                    if (string.IsNullOrWhiteSpace(Settings.ClientId))
                    {
                        ShowMessage("Twitch Api Client ID ist in den Einstellungen nicht gesetzt", new NotificationSetting(false, true));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Settings.AccessToken))
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
                twitchAPI.Settings.ClientId = Settings.ClientId;
                twitchAPI.Settings.AccessToken = Settings.AccessToken;
                twitchAPI.Settings.Secret = Settings.OAuthToken;
                twitchClient = new TwitchClient(logger: loggerfactory.CreateLogger<TwitchClient>());
                twitchClient.OnConnected += TwitchClient_OnConnected;
                twitchClient.OnNewSubscriber += TwitchClient_OnNewSubscriber;
                twitchClient.OnRaidNotification += TwitchClient_OnRaidNotification;
                twitchClient.OnUserJoined += TwitchClient_OnUserJoined;
                twitchClient.OnUserLeft += TwitchClient_OnUserLeft;
                twitchClient.OnBeingHosted += TwitchClient_OnBeingHosted;
                twitchClient.OnMessageReceived += TwitchClient_OnMessageReceived;

                twitchClient.Initialize(new ConnectionCredentials(Settings.Username, Settings.OAuthToken), Settings.ChannelToJoin);
                twitchClient.Connect();

                if (Settings.CheckForNewFollowers)
                {
                    followerService = new FollowerService(twitchAPI, checkIntervalInSeconds: 5);
                    followerService.SetChannelsByName(new List<string>() { Settings.ChannelToJoin });
                    followerService.OnNewFollowersDetected += FollowerService_OnNewFollowersDetected;
                    followerService.UpdateLatestFollowersAsync(false);
                    followerService.Start();
                }

                IsTwitchConnected = true;
            }
            catch (Exception ex)
            {
                mainViewModel.ShowError(ex);
            }
        }

        //private async Task Test()
        //{
        //    HttpClient c = new HttpClient();
        //    c.DefaultRequestHeaders.Clear();
        //    c.DefaultRequestHeaders.Add("Client-ID", "TWITCH-CLIENT-ID");
        //    c.DefaultRequestHeaders.Add("Accept", "application/vnd.twitchtv.v5+json");
        //    var w = Stopwatch.StartNew();
        //    var aall = await c.GetStringAsync("https://api.twitch.tv/kraken/chat/emoticons");
        //    dynamic a = JsonConvert.DeserializeObject(aall);
        //    var b = JsonConvert.SerializeObject(a, Formatting.Indented);
        //    w.Stop();
        //    File.WriteAllText("twitchemojis-zeit.txt", TimeSpan.FromMilliseconds(w.ElapsedMilliseconds).TotalSeconds.ToString());
        //    File.WriteAllText("twitchemojis-formated.txt", b);
        //    ;
        //}

        private void FollowerService_OnNewFollowersDetected(object sender, TwitchLib.Api.Services.Events.FollowerService.OnNewFollowersDetectedArgs e)
        {
            try
            {
                var followers = e.NewFollowers
                    .Select(x => ReplaceNickname(x.FromUserName));

                var msg = string.Join(" und ", followers);
                var msgEndIndex = msg.LastIndexOf(" und ");

                if (msgEndIndex > 0)
                    msg = msg.Substring(0, msgEndIndex);

                ShowMessage($"{msg} {(followers.Count() > 1 ? "folgen" : "folgt")} nun", Settings.NewFollowerNotification);
            }
            catch (Exception ex)
            {
                mainViewModel.ShowError(ex);
            }
        }

        #region Twitch Events
        private async void TwitchClient_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            try
            {
                if (e.ChatMessage.IsMe)
                    return;

                if (Settings.UserBlacklist.Contains(e.ChatMessage.Username, StringComparer.OrdinalIgnoreCase))
                    return;

                if (Settings.PrefixList?.Any(x => e.ChatMessage.Message.StartsWith(x, StringComparison.OrdinalIgnoreCase)) ?? false)
                    return;

                if (Settings.ReplaceBsr && bsrRegex.IsMatch(e.ChatMessage.Message))
                {
                    var match = bsrRegex.Match(e.ChatMessage.Message);
                    var bsrKey = match.Groups[1].Value;
                    var song = await GetBeatSaverSongName(bsrKey);
                    ShowMessage($"{ReplaceNickname(e.ChatMessage.Username)} wünscht sich {song}", Settings.MessageNotification);
                }
                else
                {
                    ShowChatMessage(ReplaceNickname(e.ChatMessage.Username), e.ChatMessage.Message, Settings.MessageNotification);
                }
            }
            catch (Exception ex)
            {
                mainViewModel.ShowError(ex);
            }
        }

        private void TwitchClient_OnBeingHosted(object sender, OnBeingHostedArgs e)
        {
            if (e.BeingHostedNotification.IsAutoHosted)
                return;

            ShowMessage($"{e.BeingHostedNotification.HostedByChannel} hosted mit {e.BeingHostedNotification.Viewers} Leuten", Settings.BeingHostedNotification);
        }

        private void TwitchClient_OnUserLeft(object sender, OnUserLeftArgs e)
        {
            if (Settings.UserBlacklist.Contains(e.Username, StringComparer.OrdinalIgnoreCase))
                return;

            ShowMessage($"{ReplaceNickname(e.Username)} ist weg", Settings.UserLeftNotification);
        }

        private void TwitchClient_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            if (Settings.UserBlacklist.Contains(e.Username, StringComparer.OrdinalIgnoreCase))
                return;

            if (string.Equals(e.Username, Settings.Username, StringComparison.OrdinalIgnoreCase))
                return;

            ShowMessage($"{ReplaceNickname(e.Username)} ist da", Settings.UserJoinedNotification);
        }

        private void TwitchClient_OnRaidNotification(object sender, OnRaidNotificationArgs e)
        {
            ShowMessage($"{ReplaceNickname(e.RaidNotification.DisplayName)} raidet mit {e.RaidNotification.MsgParamViewerCount} Leuten", Settings.RaidNotification);
        }

        private void TwitchClient_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            ShowMessage($"{ReplaceNickname(e.Subscriber.DisplayName)} hat abonniert", Settings.SubscriberNotification);
        }

        private void TwitchClient_OnConnected(object sender, OnConnectedArgs e)
        {
            ShowMessage("Verbunden", Settings.ClientConnectedNotification);
        }
        #endregion

        private void ShowMessage(string text, NotificationSetting notificationSetting)
        {
            if (notificationSetting.Text)
                logger.Information(text);
            if (notificationSetting.Speech)
                Speak(text);
            if (Settings.ConnectToPipeServer)
                pipeMessages.Enqueue(text);
        }

        private void ShowChatMessage(string username, string message, NotificationSetting notificationSetting)
        {
            var text = $"{username}: {message}";

            if (notificationSetting.Text)
                logger.Information(text);

            if (notificationSetting.Speech)
            {
                if (Settings.BabelSettings.DynamicallySwitchLanguage)
                    SpeakBabel(username, message);
                else
                    Speak(text);
            }

            if (Settings.ConnectToPipeServer)
                pipeMessages.Enqueue(text);
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
                pipeClient = new NamedPipeClientStream(".", Settings.PipeServerName, PipeDirection.Out);
                await pipeClient.ConnectAsync(pipeClientCTS.Token).ConfigureAwait(false);

                while (pipeClientCTS?.IsCancellationRequested == false)
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
                mainViewModel.ShowError(ex);
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

        public void Speak(string text, CultureInfo culture = null)
        {
            taskQueue.EnqueueAsync(async _ =>
            {
                try
                {
                    speech.Voice = culture == null
                        ? SpeechSynthesizer.DefaultVoice
                        : SpeechSynthesizer.AllVoices.FirstOrDefault(x => x.Language[..2] == culture.Name);

                    using var synth = await speech.SynthesizeTextToStreamAsync(text);
                    using var stream = synth.AsStreamForRead();
                    using var player = new System.Media.SoundPlayer(stream);
                    player.PlaySync();
                }
                catch (Exception ex)
                {
                    dispatcher.Invoke(() => mainViewModel.ShowError(ex));
                }
            }, false);
        }

        public void SpeakBabel(string userName, string message)
        {
            var text = $"{userName}: " + message;
            CultureInfo culture = null;

            // Babel needs atleast one character(excluding symbols like :)
            if (babelRegex.IsMatch(message))
            {
                // Detect written language
                var result = babelModel.ClassifyText(message);
                var selectedResult = result.OrderByDescending(x => x.Score).FirstOrDefault();

                if (selectedResult != null)
                {
                    if (Settings.LogBabelResult)
                        logger.Information($"Babel: {text} {string.Join(" ", result.Select(x => $"[{x.Name}:{x.Score}]"))}");

                    // Get cached culture
                    culture = Settings.BabelSettings.Languages.First(x => x.Code == selectedResult.Name).Culture;
                }
            }

            Speak(text, culture);
        }

        private string ReplaceNickname(string userName)
        {
            return Settings.UserNicknames != null &&
                Settings.UserNicknames.ContainsKey(userName) ? Settings.UserNicknames[userName] : userName;
        }

        private async Task<string> GetBeatSaverSongName(string key)
        {
            try
            {
                var beatmap = await beatSaver.Key(key);
                return beatmap.Name;
            }
            catch (Exception ex)
            {
                mainViewModel.ShowError(ex);
                return "einen Song";
            }
        }

        private void SettingsChanged()
        {
            try
            {
                if (Settings.ConnectToPipeServer)
                    Task.Run(StartPipeClient);
                else
                    DisposePipeClient();

                if (Settings.BabelSettings.DynamicallySwitchLanguage)
                {
                    babelModel = new BabelModel();
                    Settings.BabelSettings.Languages
                        .Where(x => x.Selected)
                        .Select(x => x.Code)
                        .ToList()
                        .ForEach(x => babelModel.Add(x, BabelModel._AllModel[x]));
                }
            }
            catch (Exception ex)
            {
                mainViewModel.ShowError(ex);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // Dient zur Erkennung redundanter Aufrufe.

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: verwalteten Zustand (verwaltete Objekte) entsorgen.
                    DisposePipeClient();
                    followerService?.Stop();
                    twitchClient?.Disconnect();
                }

                // TODO: nicht verwaltete Ressourcen (nicht verwaltete Objekte) freigeben
                // TODO: große Felder auf Null setzen.

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in Dispose(bool disposing) weiter oben ein.
            Dispose(true);
        }
        #endregion
    }
}
