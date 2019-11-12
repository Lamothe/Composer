using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Composer
{
    public static class Constants
    {
        public const int InfoSize = 100;
        public const int InfoMargin = 2;
        public const int BarMargin = 2;
        public const int TrackHeight = 100;
        public const int BarWidth = 200;

        public static SolidColorBrush ApplicationBackgroundBrush { get; } = new SolidColorBrush(Color.FromArgb(0x2d, 0xFF, 0xFF, 0xFF));

        public static SolidColorBrush TextBrush { get; } = new SolidColorBrush(Colors.DarkGray);
        public static SolidColorBrush BackgroundBrush { get; } = new SolidColorBrush(Color.FromArgb(0x1c, 0xFF, 0xFF, 0xFF));        
        public static SolidColorBrush BorderBrush { get; } = new SolidColorBrush(Colors.DarkGray);

        public static SolidColorBrush SelectedTextBrush { get; } = new SolidColorBrush(Colors.White);
        public static SolidColorBrush SelectedBackgroundBrush { get; } = new SolidColorBrush(Color.FromArgb(0x03, 0xFF, 0xFF, 0xFF));
        public static SolidColorBrush SelectedBorderBrush { get; } = new SolidColorBrush(Colors.White);

        public static SolidColorBrush SelectedItemTextBrush { get; } = new SolidColorBrush(Colors.White);
        public static SolidColorBrush SelectedItemBackgroundBrush { get; } = new SolidColorBrush(Colors.RoyalBlue);
        public static SolidColorBrush SelectedItemBorderBrush { get; } = new SolidColorBrush(Colors.RoyalBlue);

        public static SolidColorBrush HighlightTextBrush { get; } = new SolidColorBrush(Colors.RoyalBlue);
        public static SolidColorBrush HighlightBackgroundBrush { get; } = new SolidColorBrush(Color.FromArgb(0x04, 0xFF, 0xFF, 0xFF));
        public static SolidColorBrush HighlightBorderBrush { get; } = new SolidColorBrush(Colors.RoyalBlue);
    }
}
