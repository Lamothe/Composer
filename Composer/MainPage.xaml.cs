using Composer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
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
    public sealed partial class MainPage : Page
    {
        private string StoragePath = null;
        private Model.Song Song { get; set; }
        private Model.Metronome Metronome { get; set; }
        private bool Updated { get; set; } = false;
        private int TrackSequence { get; set; } = 0;
        private bool ScrollUpdating { get; set; } = false;
        private UI.Bar SelectedBar { get; set; } = null;
        private double Zoom { get; set; } = 0;
        public Status AudioStatus { get; set; }
        public int Position { get; set; }
        public decimal SecondsPerBar { get; set; }
        private Audio RecordingAudio { get; set; }
        private Audio PlaybackAudio { get; set; }
        private object PositionLocker = new object();
        private SolidColorBrush DefaultBrush = new SolidColorBrush(Colors.WhiteSmoke);
        private const int NumberOfBars = 20;
        private const int BarSize = 200;
        private const int InfoSize = 100;
        private const int InfoMargin = 2;
        private const int BarMargin = 2;

        public event EventHandler AudioStatusChanged;
        public event EventHandler PositionChanged;

        private async void CallUI(DispatchedHandler f) =>
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, f);

        public MainPage()
        {
            this.InitializeComponent();

            Status.Text = "Initialising ...";

            RecordButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.R });
            PlayButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.P });
            StopButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.S });
            MetronomeButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.M });
            SaveButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.S, Modifiers = Windows.System.VirtualKeyModifiers.Control });

            var copy = new KeyboardAccelerator { Key = Windows.System.VirtualKey.C, Modifiers = Windows.System.VirtualKeyModifiers.Control };
            copy.Invoked += (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null && SelectedBar.Model != null && SelectedBar.Model.Buffer != null)
                {
                    var dataPackage = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };
                    dataPackage.SetData("PCM", SelectedBar.Model.Buffer);
                    Clipboard.SetContent(dataPackage);
                }
            };
            KeyboardAccelerators.Add(copy);

            var paste = new KeyboardAccelerator { Key = Windows.System.VirtualKey.V, Modifiers = Windows.System.VirtualKeyModifiers.Control };
            paste.Invoked += async (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null && SelectedBar.Model != null)
                {
                    if (SelectedBar.Model.Buffer == null)
                    {
                        SelectedBar.Model.Buffer = new float[Song.SamplesPerBar];
                    }

                    var content = Clipboard.GetContent();
                    if (content.AvailableFormats.Contains("PCM"))
                    {
                        var data = await content.GetDataAsync("PCM");
                        var pcm = data as float[];
                        pcm.CopyTo(SelectedBar.Model.Buffer, 0);
                        SelectedBar.QueueUpdate();
                        SelectedBar.Update();
                    }
                }
            };
            KeyboardAccelerators.Add(paste);

            var delete = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Delete };
            delete.Invoked += (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null && SelectedBar.Model != null)
                {
                    SelectedBar.Model.SetEmpty();
                }
            };
            KeyboardAccelerators.Add(delete);

            var left = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Left };
            left.Invoked += (s, e) =>
            {
                e.Handled = true;
                SelectedBar?.Track.SelectPrevious(SelectedBar);
            };
            KeyboardAccelerators.Add(left);

            var right = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Right };
            right.Invoked += (s, e) =>
            {
                e.Handled = true;
                SelectedBar?.Track.SelectNext(SelectedBar);
            };
            KeyboardAccelerators.Add(right);

            var save = new KeyboardAccelerator { Key = Windows.System.VirtualKey.S, Modifiers = Windows.System.VirtualKeyModifiers.Control };
            save.Invoked += (s, e) =>
            {
                e.Handled = true;
                Save();
            };
            KeyboardAccelerators.Add(save);

            Song = new Song();

            Tracks.PointerWheelChanged += (s, e) =>
            {
                var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

                if ((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0)
                {
                    Zoom += delta * 0.01;
                }
            };

            var timer = new System.Timers.Timer(200);
            timer.Elapsed += (s, e) =>
            {
                if (Updated)
                {
                    Updated = false;
                    CallUI(() =>
                    {
                        UpdateStatus();
                        Tracks.ForEach<UI.Track>(x => x.Update());
                    });
                }
            };
            timer.Start();

            AudioStatusChanged += (s, e) => Updated = true;
            PositionChanged += (s, e) => Updated = true;

            Load();
        }

        private async void Load()
        {
            try
            {
                PlaybackAudio = await Audio.Create();
                PlaybackAudio.PositionUpdated += (s, position) => SetPosition(position);
                PlaybackAudio.Completed += (s, e) => CallUI(() => Stop());

                RecordingAudio = await Audio.Create();
                RecordingAudio.PositionUpdated += (s, position) => SetPosition(position);
                RecordingAudio.Completed += (s, e) => CallUI(() => Stop());

                SecondsPerBar = 60 * Song.BeatsPerBar / Song.BeatsPerMinute;
                Song.SamplesPerBar = (int)(PlaybackAudio.SamplesPerSecond * SecondsPerBar) / 2;

                var dummyInfoButton = new Button
                {
                    Content = " ",
                    Width = BarSize,
                    Margin = new Thickness(InfoMargin)
                };
                Timeline.Children.Add(dummyInfoButton);
                Timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                var loopButtonSize = 30;
                for (int i = 0; i < NumberOfBars; i++)
                {
                    var barGrid = new Grid();

                    var left = new Button { Content = "<", Tag = i };
                    var button = new Button { Content = $"Bar {i + 1}", Width = BarSize - loopButtonSize * 2 - BarMargin * 2 };
                    var right = new Button { Content = ">", Tag = i };

                    left.Click += (s, e) =>
                    {
                        var index = (int)left.Tag;
                        if (!Song.EndLoop.HasValue || index <= Song.EndLoop.Value)
                        {
                            Song.BeginLoop = index;
                            Updated = true;
                        }
                    };

                    right.Click += (s, e) =>
                    {
                        var index = (int)right.Tag;
                        if (!Song.BeginLoop.HasValue || index >= Song.BeginLoop.Value)
                        {
                            Song.EndLoop = index;
                            Updated = true;
                        }
                    };

                    barGrid.Children.Add(left);
                    barGrid.Children.Add(button);
                    barGrid.Children.Add(right);

                    Grid.SetColumn(left, 0);
                    Grid.SetColumn(button, 1);
                    Grid.SetColumn(right, 2);

                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(loopButtonSize) });
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(loopButtonSize) });

                    Timeline.Children.Add(barGrid);
                    Grid.SetColumn(barGrid, i + 1);
                    Timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BarSize) });
                }

                PlayButton.IsEnabled = true;
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = true;
                MetronomeButton.IsEnabled = true;
                SaveButton.IsEnabled = true;

                Status.Text = "Ready";
            }
            catch (Exception ex)
            {
                Status.Text = $"Exception: {ex.Message}";
            }
        }

        private void UpdateStatus()
        {
            var bars = Position / (decimal)Song.SamplesPerBar;
            var secondsPerBar = 60 * Song.BeatsPerBar / (decimal)Song.BeatsPerMinute;
            var seconds = (decimal)(bars * secondsPerBar);
            var text = $"{AudioStatus.ToString()}: {seconds.ToTimeString()} s";
            if (Song.BeginLoop.HasValue && Song.EndLoop.HasValue)
            {
                text += $" [Looping Bars: {Song.BeginLoop.Value + 1}-{Song.EndLoop.Value + 1}]";
            }
            Status.Text = text;
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

        private void UpdateHorizontalPosition(double offset)
        {
            if (!ScrollUpdating)
            {
                ScrollUpdating = true;
                TimelineScroll.ChangeView(offset, 0, 1, true);
                Tracks.ForEach<UI.Track>(x => x.UpdateScroll(offset));
                ScrollUpdating = false;
            }
        }

        private UI.Track AddTrack()
        {
            var model = new Track
            {
                Name = GenerateTrackName(),
                Song = Song
            };
            Song.AddTrack(model);

            var ui = new UI.Track { Model = model };

            ui.DeleteTrack += (s, e) =>
            {
                Stop();
                Song.Tracks.Remove(ui.Model);
                Tracks.Children.Remove(ui);
                int row = 0;
                Tracks.ForEach<UI.Track>(t => Grid.SetRow(t, row++));
            };

            ui.HorizontalPositionChanged += (s, offset) => UpdateHorizontalPosition(offset);
            TimelineScroll.ViewChanged += (s, e) => UpdateHorizontalPosition(TimelineScroll.HorizontalOffset);

            Tracks.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            Grid.SetRow(ui, Tracks.Children.Count());
            Grid.SetColumn(ui, 0);
            Tracks.Children.Add(ui);

            for (int count = 0; count < NumberOfBars; count++)
            {
                var barModel = new Bar { Track = model };
                model.Bars.Add(barModel);

                barModel.Update += (s1, e) => Updated = true;

                var barUI = ui.AddBar(barModel);

                barUI.PointerPressed += (s1, e) => barUI.Select(true);

                barUI.Selected += (s1, e) =>
                {
                    SelectedBar?.Select(false);
                    SelectedBar = barUI;
                };
            }

            return ui;
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            Record();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePlay();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void MetronomeButton_Checked(object sender, RoutedEventArgs e)
        {
            Metronome.Play();
        }

        private void MetronomeButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Metronome.Stop();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void ToggleRecord()
        {
            if (AudioStatus == Model.Status.Stopped)
            {
                Record();
            }
            else
            {
                Stop();
            }
        }

        private void TogglePlay()
        {
            if (AudioStatus == Model.Status.Stopped)
            {
                Play();
            }
            else
            {
                Stop();
            }
        }

        private void SetPosition(int position)
        {
            Position = position;
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Record()
        {
            if (AudioStatus == Model.Status.Stopped)
            {
                SetPosition(0);

                var track = AddTrack();
                track.IsRecording = true;
                RecordingAudio.Record(track.Model);
                SetAudioStatus(Model.Status.Recording);
            }
        }

        private void Play()
        {
            if (AudioStatus == Model.Status.Stopped)
            {
                if (Song.Tracks.Any())
                {
                    PlaybackAudio.Play(Song);
                    SetAudioStatus(Model.Status.Playing);
                }
            }
        }

        private void Stop()
        {
            if (AudioStatus != Model.Status.Stopped)
            {
                Tracks.ForEach<UI.Track>(x => x.IsRecording = false);
                RecordingAudio.Stop();
                PlaybackAudio.Stop();

                SetAudioStatus(Model.Status.Stopped);
            }
        }

        private void SetAudioStatus(Status status)
        {
            if (AudioStatus != status)
            {
                AudioStatus = status;
                AudioStatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async void Save()
        {
            StorageFolder folder = null;

            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            folderPicker.FileTypeFilter.Add("*");

            folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            StoragePath = folder.Name;

            Status.Text = "Saving ...";
            Audio.Save(Song, folder);
            Status.Text = $"Saved to {StoragePath}";
        }
    }
}
