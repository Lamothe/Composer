using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Composer.UI
{
    public sealed partial class Bar : UserControl
    {
        public Model.Bar Model { get; set; }
        private List<Line> Lines { get; set; } = new List<Line>();
        private bool UpdateUI = false;
        private int PixelInterval = 1;

        public Bar()
        {
            this.InitializeComponent();
        }

        public void QueueUpdate()
        {
            UpdateUI = true;
        }

        public void Update()
        {
            if (UpdateUI)
            {
                UpdateUI = false;

                var numberOfLines = ActualWidth / PixelInterval;
                var bufferInterval = (int)(Model.Buffer.Length / numberOfLines);

                for (int i = 0; i < numberOfLines; i++)
                {
                    if (i >= Lines.Count())
                    {
                        var newLine = new Line();
                        Lines.Add(newLine);
                        Canvas.Children.Add(newLine);
                    }

                    var line = Lines[i];

                    var amplitude = Model.Buffer.Skip(i * bufferInterval).Take(bufferInterval).Max();
                    var y = (int)(amplitude * ActualHeight / 2);

                    line.X1 = i * PixelInterval;
                    line.Y1 = ActualHeight / 2 - y;
                    line.X2 = i * PixelInterval;
                    line.Y2 = ActualHeight / 2 + (y == 0 ? 1 : y);
                    line.Stroke = new SolidColorBrush(Colors.LightGray);
                }
            }
        }
    }
}
