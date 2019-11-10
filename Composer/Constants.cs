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

        public static SolidColorBrush DefaultBrush { get; } = new SolidColorBrush(Colors.WhiteSmoke);
        public static SolidColorBrush RedBrush { get; } = new SolidColorBrush(Colors.Red);
        public static SolidColorBrush GreenBrush { get; } = new SolidColorBrush(Colors.Green);
        public static SolidColorBrush BlueBrush { get; } = new SolidColorBrush(Colors.Blue);
        public static SolidColorBrush YellowBrush { get; } = new SolidColorBrush(Colors.Yellow);
    }
}
