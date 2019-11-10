﻿using System;
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
        private int lineCount = 0;

        public bool IsHovering { get; set; } = false;
        private bool IsSelected { get; set; } = false;

        public event EventHandler<Bar> Selected;

        private Canvas Canvas { get; set; }

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

            Update();
        }

        public Core.Model.Bar Model { get; private set; }

        public UI.Track Track { get; private set; }

        public void Select()
        {
            IsSelected = true;
            Update();
        }

        public void Deselect()
        {
            IsSelected = false;
            Update();
        }

        public void Update()
        {
            var numberOfLines = ActualWidth / LineWidth;

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
