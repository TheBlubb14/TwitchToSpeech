using System.Collections.ObjectModel;

namespace TwitchToSpeech.Model
{
    public class MenuGroup
    {
        public string Name { get; set; }

        public ObservableCollection<MenuItem> Items { get; set; }
    }
}
