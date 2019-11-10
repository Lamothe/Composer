using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Composer.UI
{
    public class Track : Grid
    {
        public Core.Model.Track Model { get; private set; }
        public event EventHandler<Track> Deleted;

        public Track(Core.Model.Track model)
        {
            Model = model;
            Width = Constants.TrackHeight;
            Height = Constants.TrackHeight;
            BorderBrush = Constants.TextBrush;
            BorderThickness = new Thickness(1);

            var deleteButton = new Button { Content = "Delete" };
            deleteButton.Click += (object sender, RoutedEventArgs e) => Deleted?.Invoke(sender, this);
            Children.Add(new TextBlock { Text = model?.Name });
            Children.Add(deleteButton);
        }
    }
}
