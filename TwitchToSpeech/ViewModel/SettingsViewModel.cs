using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using System.Windows.Input;
using TwitchToSpeech.Model;

namespace TwitchToSpeech.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        public ICommand LoadedCommand { get; set; }

        public Settings Settings { get; set; }

        public SettingsViewModel()
        {
            if (!IsInDesignMode)
            {
                LoadedCommand = new RelayCommand(Loaded);
            }
        }

        private void Loaded()
        {
            // Make a copy of settings, so we can discard the changes if the user cancels the edit
            this.Settings = JsonConvert.DeserializeObject<Settings>(JsonConvert.SerializeObject(Settings.Instance));
        }
    }
}
