using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Composer
{
    public static class Extensions
    {
        public static List<T> GetChildren<T>(this Panel panel)
        {
            return panel.Children.Cast<T>().ToList();
        }

        public static void ForEach<T>(this Panel panel, Action<T> action)
        {
            panel.GetChildren<T>().ForEach(action);
        }

        public static string ToTimeString(this decimal d)
        {
            return d.ToString("0.00");
        }
    }
}
