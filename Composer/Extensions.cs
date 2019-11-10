using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Composer
{
    public static class Extensions
    {
        public static List<T> GetChildren<T>(this Panel panel)
        {
            return panel?.Children.Cast<T>().ToList();
        }

        public static void ForEach<T>(this Panel panel, Action<T> action)
        {
            panel.GetChildren<T>().ForEach(action);
        }

        public static string ToTimeString(this decimal d)
        {
            return d.ToString("0.00");
        }

        public static void ScrollToElement(this ScrollViewer scrollViewer, UIElement element,
            bool isVerticalScrolling = true, bool smoothScrolling = true, float? zoomFactor = null)
        {
            var transform = element.TransformToVisual((UIElement)scrollViewer.Content);
            var position = transform.TransformPoint(new Point(0, 0));

            if (isVerticalScrolling)
            {
                scrollViewer.ChangeView(null, position.Y, zoomFactor, !smoothScrolling);
            }
            else
            {
                scrollViewer.ChangeView(position.X, null, zoomFactor, !smoothScrolling);
            }
        }

        public static UIElement GetChildAt(this Grid grid, int row, int column)
        {
            return grid.GetChildren<FrameworkElement>().FirstOrDefault(x => Grid.GetRow(x) == row && Grid.GetColumn(x) == column);
        }
    }
}
