using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Windows.Input;
using TwitchToSpeech.Model;

namespace TwitchToSpeech.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        public ICommand LoadedCommand { get; set; }

        public ICommand OpenOAuthWebsiteCommand { get; set; }

        public Settings Settings { get; set; }

        public SettingsViewModel()
        {
            if (!IsInDesignMode)
            {
                LoadedCommand = new RelayCommand(Loaded);
                OpenOAuthWebsiteCommand = new RelayCommand(OpenOAuthWebsite);
            }
        }

        private void Loaded()
        {
            // Make a copy of settings, so we can discard the changes if the user cancels the edit
            this.Settings = JsonConvert.DeserializeObject<Settings>(JsonConvert.SerializeObject(Settings.Instance));
        }

        private void OpenOAuthWebsite()
        {
            Process.Start(new ProcessStartInfo("https://twitchapps.com/tmi/")
            {
                // Explicitly set UseShellExecute to true, 
                // because in .NetFramework the default value is true but in
                // .NetCore the default value changed to false
                // See: https://github.com/dotnet/winforms/issues/1520#issuecomment-515899341
                // and https://github.com/dotnet/corefx/issues/24704
                UseShellExecute = true
            });
        }
    }
}
