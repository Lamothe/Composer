using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Composer.UI
{
    public class Bar : Grid
    {
        private int LineWidth = 1;
        private static Color BarColor = new Color { A = 0xFF, R = 0x30, G = 0x30, B = 0x30 };
        private static Color SelectedBarColor = new Color { A = 0xFF, R = 0x40, G = 0x40, B = 0x20 };
        private static Brush BarBrush = new SolidColorBrush(BarColor);
        private static Brush SelectedBarBrush = new SolidColorBrush(SelectedBarColor);
        private int lineCount = 0;
        private Canvas Canvas { get; set; }

        public Bar(Core.Model.Bar model, UI.Track track)
        {
            Model = model;
            Track = track;

            Canvas = new Canvas();

            BorderBrush = Constants.DefaultBrush;
            BorderThickness = new Thickness(2);

            Children.Add(Canvas);
            Grid.SetColumn(Canvas, 0);
            Grid.SetRow(Canvas, 0);
        }

        public Core.Model.Bar Model { get; private set; }

        public UI.Track Track { get; private set; }

        public void Update()
        {
            var numberOfLines = ActualWidth / LineWidth;

            if (Model.Buffer == null)
            {
                Canvas.Children.Clear();
            }
            else
            {
                var bufferInterval = (int)(Model.Buffer.Length / numberOfLines);

                for (int i = lineCount; i < numberOfLines; i++)
                {
                    var line = new Line();

                    var amplitude = Model.Buffer.Skip(i * bufferInterval).Take(bufferInterval).Max();
                    var y = (int)(amplitude * ActualHeight / 2);

                    line.X1 = i * LineWidth;
                    line.Y1 = ActualHeight / 2 - y;
                    line.X2 = i * LineWidth;
                    line.Y2 = ActualHeight / 2 + (y == 0 ? 1 : y);
                    line.Stroke = new SolidColorBrush(Colors.LightGray);
                    line.StrokeThickness = LineWidth;

                    Canvas.Children.Add(line);

                    lineCount++;
                }
            }
        }
    }
}
