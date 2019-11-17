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
        private int LineWidth = 2;
        private int LineCount = 0;

        public bool IsHovering { get; set; } = false;
        private bool IsSelected { get; set; } = false;
        public bool FullUpdate { get; set; }

        public event EventHandler<Bar> Selected;
        public event EventHandler<Bar> Deselected;

        private Canvas Canvas { get; set; }

        public Core.Model.Bar Model { get; private set; }

        public UI.Track Track { get; private set; }

        public Bar(Core.Model.Bar model, UI.Track track)
        {
            Model = model;
            Track = track;

            Canvas = new Canvas();

            BorderThickness = new Thickness(1);

            Children.Add(Canvas);
            Grid.SetColumn(Canvas, 0);
            Grid.SetRow(Canvas, 0);

            PointerEntered += (s, e) => { IsHovering = true; Update(); };
            PointerExited += (s, e) => { IsHovering = false; Update(); };
            PointerPressed += (s, e) => Select();

            Update();
        }

        public void Select()
        {
            IsSelected = true;
            Selected?.Invoke(null, this);
            Update();
        }

        public void Deselect()
        {
            IsSelected = false;
            Deselected?.Invoke(this, this);
            Update();
        }

        public void Update()
        {
            if (FullUpdate)
            {
                Canvas.Children.Clear();
            }

            if (IsSelected)
            {
                BorderBrush = Constants.SelectedBorderBrush;
                Background = Constants.SelectedBackgroundBrush;
            }
            else if (IsHovering)
            {
                BorderBrush = Constants.HighlightBorderBrush;
                Background = Constants.HighlightBackgroundBrush;
            }
            else
            {
                BorderBrush = Constants.BorderBrush;
                Background = Constants.BackgroundBrush;
            }

            var numberOfLinesPerBar = ActualWidth / LineWidth;
            var bufferInterval = (int)(Model.Buffer.Length / numberOfLinesPerBar);
            var startLine = FullUpdate ? 0 : LineCount;
            var endLine = numberOfLinesPerBar * Model.Length / Model.Buffer.Length;

            for (int i = startLine; i < endLine; i++)
            {
                var amplitude = Model.Buffer.Skip(i * bufferInterval).Take(bufferInterval).Max();
                var y = (int)(amplitude * ActualHeight / 4);
                var line = new Line();

                line.X1 = i * LineWidth;
                line.Y1 = (ActualHeight - y) / 2;
                line.X2 = i * LineWidth;
                line.Y2 = (ActualHeight + (y == 0 ? 1 : y)) / 2;
                line.Stroke = Constants.TextBrush;
                line.StrokeThickness = LineWidth;

                Canvas.Children.Add(line);

                LineCount++;
            }

            FullUpdate = false;
        }
    }
}
