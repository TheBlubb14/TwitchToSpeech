using System;
using System.Windows;
using System.Windows.Controls;

namespace TwitchToSpeech.Model
{
    // https://stackoverflow.com/questions/2984803/how-to-automatically-scroll-scrollviewer-only-if-the-user-did-not-change-scrol
    public static class AttachedProperties
    {
        public static readonly DependencyProperty ScrollToEndProperty =
            DependencyProperty.RegisterAttached("ScrollToEnd",
                typeof(bool),
                typeof(ScrollViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, ScrollToEndChanged));

        private static bool _autoScroll;

        private static void ScrollToEndChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ScrollViewer scroll)
            {
                if ((e.NewValue != null) && (bool)e.NewValue)
                    scroll.ScrollChanged += ScrollChanged;
                else
                    scroll.ScrollChanged -= ScrollChanged;
            }
            else
            {
                throw new InvalidOperationException($"The attached {nameof(ScrollToEndProperty)} property can only be applied to ScrollViewer instances.");
            }
        }

        public static bool GetScrollToEnd(ScrollViewer scroll)
        {
            if (scroll is null)
                throw new ArgumentNullException(nameof(scroll));

            return (bool)scroll.GetValue(ScrollToEndProperty);
        }

        public static void SetScrollToEnd(ScrollViewer scroll, bool alwaysScrollToEnd)
        {
            if (scroll is null)
                throw new ArgumentNullException(nameof(scroll));

            scroll.SetValue(ScrollToEndProperty, alwaysScrollToEnd);
        }

        private static void ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!(sender is ScrollViewer scroll))
                throw new InvalidOperationException($"The attached {nameof(ScrollToEndProperty)} property can only be applied to ScrollViewer instances.");

            if (e.ExtentHeightChange == 0)
            {
                if (scroll.VerticalOffset == scroll.ScrollableHeight)
                {
                    // Scroll bar is in bottom
                    _autoScroll = true;
                }
                else
                {
                    // Scroll bar isn't in bottom
                    _autoScroll = false;
                }
            }

            if (_autoScroll && e.ExtentHeightChange != 0)
            {
                // Content changed and auto-scroll mode set
                scroll.ScrollToVerticalOffset(scroll.ExtentHeight);
            }
        }
    }
}
