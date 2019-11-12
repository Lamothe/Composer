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

        public static T GetChildAt<T>(this Grid grid, int row, int column)
            where T : FrameworkElement
        {
            return grid.GetChildren<T>(x => Grid.GetRow(x) == row && Grid.GetColumn(x) == column).FirstOrDefault();
        }

        public static List<T> GetChildren<T>(this Grid grid, Func<T, bool> f)
        {
            return grid.GetChildren<T>().Where(f).ToList();
        }

        public static void DeleteChildren(this Grid grid, Func<FrameworkElement, bool> f)
        {
            grid.GetChildren(f).ForEach(c => grid.Children.Remove(c));
        }
    }
}
