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
    public sealed partial class Track : UserControl
    {
        public Model.Track Model { get; set; }
        public uint BarWidth { get; set; } = 200;

        public event EventHandler DeleteTrack;
        public event EventHandler<double> ScrollViewChanged;

        private static Color TrackColor = new Color { A = 0xFF, R = 0x10, G = 0x10, B = 0x10 };
        private static Color RecordingTrackColor = new Color { A = 0xFF, R = 0x30, G = 0x20, B = 0x20 };
        private static Brush TrackBrush = new SolidColorBrush(TrackColor);
        private static Brush RecordingTrackBrush = new SolidColorBrush(RecordingTrackColor);

        public Track()
        {
            this.InitializeComponent();

            DeleteButton.Click += (s, e) =>
            {
                Bars.ForEach<UI.Bar>(x => x.Delete());
                DeleteTrack?.Invoke(this, EventArgs.Empty);
            };
            Scroll.ViewChanged += (s, e) => ScrollViewChanged?.Invoke(this, Scroll.HorizontalOffset);
            MuteButton.Checked += (s, e) => Model.IsMuted = true;
            MuteButton.Unchecked += (s, e) => Model.IsMuted = false;
        }

        private Brush GetBackground()
        {
            if (Model.Status == Composer.Model.Status.Recording)
            {
                return RecordingTrackBrush;
            }

            return TrackBrush;
        }

        public void Update()
        {
            foreach (var ui in Bars.Children)
            {
                (ui as UI.Bar).Update();
            }

            TrackGrid.Background = GetBackground();
            Info.Text = $"{Model.Name}";
        }

        public UI.Bar AddBar(Model.Bar model)
        {
            var ui = new UI.Bar
            {
                Model = model,
                Width = BarWidth
            };

            ui.Deleted += (s, e) =>
            {
                ui.Model.SetEmpty();
                ui.QueueUpdate();
                Update();
            };

            model.Update += (s, e) => ui.QueueUpdate();

            Bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BarWidth) });
            Grid.SetRow(ui, 0);
            Grid.SetColumn(ui, Bars.Children.Count());
            Bars.Children.Add(ui);

            return ui;
        }

        public void UpdateScroll(double horizontalOffset)
        {
            Scroll.ChangeView(horizontalOffset, 0, 1, true);
        }

        public void SelectPrevious(UI.Bar current)
        {
            var index = Bars.Children.IndexOf(current);
            if (index - 1 >= 0)
            {
                (Bars.Children[index - 1] as UI.Bar).Select(true);
            }
        }

        public void SelectNext(UI.Bar current)
        {
            var index = Bars.Children.IndexOf(current);
            if (index + 1 < Bars.Children.Count())
            {
                (Bars.Children[index + 1] as UI.Bar).Select(true);
            }
        }
    }
}
