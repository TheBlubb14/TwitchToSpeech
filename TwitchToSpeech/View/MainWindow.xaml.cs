using System.Windows;
using TwitchToSpeech.ViewModel;

namespace TwitchToSpeech.View
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ((MainViewModel)DataContext).SnackbarMessageQueue = MainSnackbar.MessageQueue;
        }
    }
}
