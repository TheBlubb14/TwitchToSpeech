using CommonServiceLocator;
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
        public ICommand OpenOAuthWebsiteCommand { get; set; }

        public Settings Settings { get; set; }

        public SettingsViewModel()
        {
            if (!IsInDesignMode)
            {
                OpenOAuthWebsiteCommand = new RelayCommand(OpenOAuthWebsite);
                Settings = ServiceLocator.Current.GetInstance<Settings>();
            }
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
