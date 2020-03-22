using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MaterialDesignThemes.Wpf;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using TwitchToSpeech.Model;
using TwitchToSpeech.View;
using TwitchToSpeech.View.Settings;

namespace TwitchToSpeech.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        #region Menu
        public ObservableCollection<MenuGroup> Menu { get; set; }

        public ICommand MenuClickedCommand { get; set; }
        #endregion

        public string Title { get; set; }

        public object CurrentView { get; set; }

        public ICommand TitleClickedCommand { get; set; }

        public ICommand MenuItemExitCommand { get; set; }

        public ICommand LoadedCommand { get; set; }

        public ICommand ClosedCommand { get; set; }

        public ISnackbarMessageQueue SnackbarMessageQueue { get; set; }

        public ICommand KeyDownCommand { get; set; }

        private readonly ObservableCollection<Exception> StartupExceptions = new ObservableCollection<Exception>();

        private readonly CockpitControl cockpitControl;

        public MainViewModel()
        {
            Title = "Twitch to Speech";

            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
            }
            else
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs\\log.txt")
                    .CreateLogger();

                // Code runs "for real"
                this.PropertyChanged += this.MainViewModel_PropertyChanged;
                LoadedCommand = new RelayCommand(Loaded);
                ClosedCommand = new RelayCommand(Closed);
                KeyDownCommand = new RelayCommand<KeyEventArgs>(KeyDown);
                MenuItemExitCommand = new RelayCommand(Application.Current.Shutdown);

                MenuClickedCommand = new RelayCommand<object>(MenuClicked);
                TitleClickedCommand = new RelayCommand(TitleClicked);

                cockpitControl = new CockpitControl();
                CurrentView = cockpitControl;

                Menu = new ObservableCollection<MenuGroup>()
                {
                    new MenuGroup()
                    {
                        Name = "Settings",
                        Items = new ObservableCollection<MenuItem>()
                        {
                            new MenuItem("Twitch", PackIconKind.Settings, new TwitchControl()),
                            new MenuItem("Twitch API", PackIconKind.Settings, new TwitchApiControl()),
                            new MenuItem("Filter", PackIconKind.Settings, new FilterControl()),
                            new MenuItem("Notifications Text", PackIconKind.Settings, new NotificationTextControl()),
                            new MenuItem("Notifications Speech", PackIconKind.Settings, new NotificationSpeechControl()),
                            new MenuItem("Chat", PackIconKind.Settings, new ChatControl()),
                            new MenuItem("Babel", PackIconKind.Settings, new BabelControl()),
                            new MenuItem("Pipe", PackIconKind.Settings, new PipeControl())
                        }
                    }
                };
            }
        }

        public void TitleClicked()
        {
            CurrentView = cockpitControl;
        }

        public void MenuClicked(object content)
        {
            CurrentView = content;
        }

        private void Loaded()
        {
            try
            {
                //SettingsChanged();
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
                // TODO: Dispose all controls
            }
            catch (Exception)
            {
                // Doesnt make sense to show an error if all is already closed
            }
        }

        private void KeyDown(KeyEventArgs e)
        {
            static bool IsControl() => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (e.Key == Key.E && IsControl())
                MenuItemExitCommand.Execute(null);
        }

        private void MainViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SnackbarMessageQueue):
                    ShowStartupExceptions();
                    break;
            }
        }

        public void ShowError(Exception exception, [CallerMemberName]string caller = null)
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
    }
}