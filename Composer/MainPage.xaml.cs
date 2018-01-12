using Composer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Composer
{
    public static class DecimalExtension
    {
        public static string ToTimeString(this decimal d)
        {
            return d.ToString("0.00");
        }
    }

    public sealed partial class MainPage : Page
    {
        private Model.Song Song { get; set; }
        private Model.Audio Audio { get; set; }
        private Model.Metronome Metronome { get; set; }
        private bool Updated { get; set; } = false;
        private int TrackSequence { get; set; } = 0;

        private double Zoom = 0;

        public MainPage()
        {
            this.InitializeComponent();

            Song = new Song();

            Tracks.PointerWheelChanged += (s, e) =>
            {
                var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

                if ((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0)
                {
                    Zoom += delta * 0.01;
                }
            };

            var timer = new System.Timers.Timer(100);
            timer.Elapsed += (s, e) =>
            {
                if (Updated)
                {
                    Updated = false;
                    CallUI(() =>
                    {
                        Tracks.Children.ToList().ForEach(x => (x as UI.Track).Update());
                    });
                }
            };
            timer.Start();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private async void Load()
        {
            Audio = await Model.Audio.Create();
            Metronome = new Model.Metronome(Audio);

            PlayButton.IsEnabled = true;
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            MetronomeButton.IsEnabled = true;
        }

        private string GenerateTrackName()
        {
            while (true)
            {
                var name = $"Track {++TrackSequence}";
                if (!Song.Tracks.Any(x => x.Name == name))
                {
                    return name;
                }
            }
        }

        private UI.Track AddTrack()
        {
            var encodingProperties = Audio.Graph.EncodingProperties;
            encodingProperties.ChannelCount = 1;

            var model = new Model.Track()
            {
                Name = GenerateTrackName(),
                FrameInputNode = Audio.Graph.CreateFrameInputNode(encodingProperties),
                FrameOutputNode = Audio.Graph.CreateFrameOutputNode(encodingProperties)
            };

            model.Stop();

            Audio.DeviceInputNode.AddOutgoingConnection(model.FrameOutputNode);
            model.FrameInputNode.AddOutgoingConnection(Audio.DeviceOutputNode);

            Audio.Graph.QuantumStarted += (g, e) => model.Write();
            model.FrameInputNode.QuantumStarted += (g, e) => model.Read(e.RequiredSamples);
            var samplesPerMinute = 60 * Audio.SamplesPerSecond;
            model.SamplesPerBar = (uint)(samplesPerMinute * Song.BeatsPerBar) / Song.BeatsPerMinute;

            Song.Tracks.Add(model);
            model.TrackInformationChanged += (s, e) => Updated = true;

            var ui = new UI.Track { Model = model };
            ui.PointerPressed += (s, e) => SelectTrack(ui);
            ui.DeleteTrack += (s, e) => DeleteTrack(ui);
            Tracks.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            Grid.SetRow(ui, Tracks.Children.Count());
            Grid.SetColumn(ui, 0);
            Tracks.Children.Add(ui);

            model.BarAdded += (s, bar) =>
            {
                Updated = true;
                bar.Update += (s2, e) => Updated = true;
                CallUI(() =>
                {
                    var barUI = ui.AddBar(bar);
                    barUI.PointerPressed += (s1, e) =>
                    {
                        SelectTrack(barUI.Track);
                        barUI.Track.SelectBar(barUI);
                    };
                });
            };

            return ui;
        }

        private async void CallUI(DispatchedHandler x) =>
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, x);

        private void DeleteTrack(UI.Track ui)
        {
            ui.Model.Stop();
            Song.Tracks.Remove(ui.Model);
            Tracks.Children.Remove(ui);
            int row = 0;
            Tracks.Children.ToList().ForEach(track => Grid.SetRow(track as UI.Track, row++));
        }

        private void SelectTrack(UI.Track ui)
        {
            Tracks.Children.ToList().ForEach(x => (x as UI.Track).Select(x == ui));
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            Song.Tracks.ForEach(x => x.Stop());

            var track = AddTrack();
            track.Model.Record();
            SelectTrack(track);

            Audio.Start();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Song.Tracks.ForEach(x =>
            {
                x.Play();
                Audio.Start();
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Song.Tracks.ForEach(x =>
            {
                x.Stop();
                Audio.Stop();
            });
        }

        private void MetronomeButton_Checked(object sender, RoutedEventArgs e)
        {
            Metronome.Play();
        }

        private void MetronomeButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Metronome.Stop();
        }
    }
}
