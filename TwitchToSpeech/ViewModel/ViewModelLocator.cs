using CommonServiceLocator;
using GalaSoft.MvvmLight.Ioc;
using TwitchToSpeech.Model;

namespace TwitchToSpeech.ViewModel
{
    public sealed class ViewModelLocator
    {
        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();
        public SettingsViewModel SettingsViewModel => ServiceLocator.Current.GetInstance<SettingsViewModel>();
        public CockpitViewModel CockpitViewModel => ServiceLocator.Current.GetInstance<CockpitViewModel>();

        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            SimpleIoc.Default.Register(Settings.Load);
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<CockpitViewModel>();
        }
    }
}
