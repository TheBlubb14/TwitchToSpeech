using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;
using System.Windows.Media;

namespace TwitchToSpeech.Behaviors
{
    public class ScrollToBehavior : Behavior<ListBox>
    {
        public bool Enabled
        {
            get => (bool)GetValue(EnabledProperty);
            set => SetValue(EnabledProperty, value);
        }

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.Register("Enabled", typeof(bool), typeof(ScrollToBehavior), new PropertyMetadata(true));

        public ScrollTo ScrollMode
        {
            get => (ScrollTo)GetValue(ScrollProperty);
            set => SetValue(ScrollProperty, value);
        }

        public static readonly DependencyProperty ScrollProperty =
            DependencyProperty.Register("Scroll", typeof(ScrollTo), typeof(ScrollToBehavior), new PropertyMetadata(ScrollTo.Bottom));

        protected override void OnAttached()
        {
            if (AssociatedObject.ItemsSource is INotifyCollectionChanged collection)
                collection.CollectionChanged += Collection_CollectionChanged;

            base.OnAttached();
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject.ItemsSource is INotifyCollectionChanged collection)
                collection.CollectionChanged -= Collection_CollectionChanged;

            base.OnDetaching();
        }

        private void Collection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Enabled)
                return;

            if (VisualTreeHelper.GetChild(AssociatedObject, 0) is Border border &&
                VisualTreeHelper.GetChild(border, 0) is ScrollViewer scrollViewer)
            {
                switch (ScrollMode)
                {
                    case ScrollTo.Top:
                        scrollViewer.ScrollToTop();
                        break;

                    case ScrollTo.Bottom:
                        scrollViewer.ScrollToBottom();
                        break;

                    case ScrollTo.End:
                        scrollViewer.ScrollToEnd();
                        break;

                    case ScrollTo.Home:
                        scrollViewer.ScrollToHome();
                        break;

                    case ScrollTo.LeftEnd:
                        scrollViewer.ScrollToLeftEnd();
                        break;

                    case ScrollTo.RightEnd:
                        scrollViewer.ScrollToRightEnd();
                        break;
                }
            }
        }

        public enum ScrollTo
        {
            Top,
            Bottom,
            End,
            Home,
            LeftEnd,
            RightEnd,
        }
    }
}
