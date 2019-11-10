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
        private Core.Model.Song Song { get; set; }
        private bool Updated { get; set; } = false;
        private int TrackSequence { get; set; } = 0;
        private UI.Bar SelectedBar { get; set; } = null;
        private double Zoom { get; set; } = 0;
        public Core.Model.Status AudioStatus { get; set; }
        public decimal SecondsPerBar { get; set; }
        private Core.Model.IAudio Audio { get; set; }
        private int CurrentPosition { get; set; }
        private UI.Bar UpdateBar { get; set; }

        public MainPage()
        {
            this.InitializeComponent();

            SetStatus("Initialising ...");

            Background = Constants.BackgroundBrush;

            RecordButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.R });
            PlayButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.P });
            StopButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.S });
            SaveButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.S, Modifiers = Windows.System.VirtualKeyModifiers.Control });
            LoadButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.O, Modifiers = Windows.System.VirtualKeyModifiers.Control });

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

            var save = new KeyboardAccelerator { Key = Windows.System.VirtualKey.S, Modifiers = Windows.System.VirtualKeyModifiers.Control };
            save.Invoked += (s, e) =>
            {
                e.Handled = true;
                Save();
            };
            KeyboardAccelerators.Add(save);

            var console = new KeyboardAccelerator { Key = Windows.System.VirtualKey.O, Modifiers = Windows.System.VirtualKeyModifiers.Control };
            console.Invoked += (s, e) =>
            {
                e.Handled = true;
                ToggleOutputwindow();
            };
            KeyboardAccelerators.Add(console);

            Song = new Core.Model.Song();

            Tracks.PointerWheelChanged += (s, e) =>
            {
                var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

                if ((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0)
                {
                    Zoom += delta * 0.01;
                }
            };

            Song.TrackAdded += (sender, track) => OnTrackAdded(track);
            Song.TrackRemoved += (sender, track) => OnTrackRemoved(track);

            var timer = new System.Timers.Timer(200);
            timer.Elapsed += (s, e) =>
            {
                if (Updated)
                {
                    Updated = false;
                    UI.Utilities.CallUI(() =>
                    {
                        UpdatePosition(CurrentPosition);
                        UpdateBar?.Update();
                    });
                }
            };
            timer.Start();

            Init();
        }

        private void AddLog(string message)
        {
            UI.Utilities.CallUIIdle((f) => Log.Text += message + Environment.NewLine);
        }

        private void UpdatePosition(int position)
        {
            var bars = position / (decimal)Song.SamplesPerBar;
            var secondsPerBar = 60 * Song.BeatsPerBar / (decimal)Song.BeatsPerMinute;
            var seconds = (decimal)(bars * secondsPerBar);
            var text = $"{seconds.ToTimeString()} s";
            if (Song.BeginLoop.HasValue && Song.EndLoop.HasValue)
            {
                text = $" [Loop: {Song.BeginLoop.Value + 1}-{Song.EndLoop.Value + 1}] " + text;
            }
            Position.Text = text;
        }

        private void SetPosition(int position)
        {
            CurrentPosition = position;
            Updated = true;
        }

        private void SetStatus(string message)
        {
            Status.Text = message;
            AddLog(message);
        }

        private async void Init()
        {
            try
            {
                Audio = await UwpAudio.Create();
                Audio.PositionUpdated += (s, position) => SetPosition(position);
                Audio.Stopped += (sender, song) => OnStopped();
                Audio.Playing += (sender, song) => OnPlaying(song);
                Audio.Recording += (sender, track) => OnRecording(track);

                SecondsPerBar = 60 * Song.BeatsPerBar / Song.BeatsPerMinute;
                Song.SamplesPerBar = (int)(Audio.SamplesPerSecond * SecondsPerBar) / 2;

                for (var bpm = 50; bpm < 300; bpm++)
                {
                    ComboBpm.Items.Add(bpm);
                }
                ComboBpm.SelectedItem = Song.BeatsPerMinute;

                for (var bpb = 2; bpb < 10; bpb++)
                {
                    ComboBeatsPerBar.Items.Add(bpb);
                }
                ComboBeatsPerBar.SelectedItem = Song.BeatsPerBar;

                PlayButton.IsEnabled = true;
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
                ComboBpm.IsEnabled = true;
                ComboBeatsPerBar.IsEnabled = true;

                SetStatus("Ready");
            }
            catch (Exception ex)
            {
                SetStatus($"Exception: {ex.Message}");
            }
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

        private void OnTrackRemoved(Core.Model.Track track)
        {
            Stop();
            Tracks.GetChildren<UI.Track>()
                .Where(x => x.Model == track)
                .ToList()
                .ForEach(x => Tracks.Children.Remove(x));

            int row = 0;
            Tracks.ForEach<UI.Track>(t => Grid.SetRow(t, row++));

            AddLog("Bar removed");
        }

        private void OnTrackAdded(Core.Model.Track track)
        {
            track.BarAdded += (sender, bar) => UI.Utilities.CallUIIdle((f) => BarAdded(bar));
            track.Name = GenerateTrackName();
            var numberOfRows = BarsContainer.RowDefinitions.Count();

            TrackHeaders.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Constants.TrackHeight) });
            BarsContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Constants.TrackHeight) });

            var ui = new UI.Track(track);
            Grid.SetRow(ui, numberOfRows);
            Grid.SetColumn(ui, 0);
            TrackHeaders.Children.Add(ui);

            AddLog("Track added");
        }

        private void BarAdded(Core.Model.Bar bar)
        {
            var track = TrackHeaders.GetChildren<UI.Track>().SingleOrDefault(t => t.Model == bar.Track);

            var row = Grid.GetRow(track);
            var column = bar.Track.Bars.IndexOf(bar);

            while (column >= BarsContainer.ColumnDefinitions.Count)
            {
                BarsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Constants.BarWidth) });
            }

            var ui = new UI.Bar(bar, track);
            ui.PointerPressed += (s, e) =>
            {
                if (SelectedBar != null)
                {
                    SelectedBar.IsSelected = false;
                    SelectedBar.Update();
                }
                SelectedBar = ui;
                SelectedBar.IsSelected = true;
                SelectedBar.Update();
            };
            bar.Updated += (sender, b) => UpdateBar = ui;
            Grid.SetRow(ui, row);
            Grid.SetColumn(ui, column);

            BarsContainer.Children.Add(ui);

            AddLog("Bar added");
        }

        private void OnStopped()
        {
            SetAudioStatus(Core.Model.Status.Stopped);
        }

        private void OnPlaying(Core.Model.Song song)
        {
            SetAudioStatus(Core.Model.Status.Playing);
        }

        private void OnRecording(Core.Model.Track track)
        {
            SetAudioStatus(Core.Model.Status.Recording);
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private void TogglePlay()
        {
            if (AudioStatus == Core.Model.Status.Stopped)
            {
                Play();
            }
            else
            {
                Stop();
            }
        }

        private void Record()
        {
            if (AudioStatus == Core.Model.Status.Stopped)
            {
                var track = Song.AddTrack();
                Audio.Record(track);
            }
        }

        private void Play()
        {
            if (AudioStatus == Core.Model.Status.Stopped)
            {
                if (Song.Tracks.Any())
                {
                    Audio.Play(Song);
                }
            }
        }

        private void Stop()
        {
            if (AudioStatus != Core.Model.Status.Stopped)
            {
                Audio.Stop();
            }
        }

        private void SetAudioStatus(Core.Model.Status status)
        {
            if (AudioStatus != status)
            {
                AudioStatus = status;
                AddLog($"Status: {status}");
            }
        }

        private void ToggleOutputwindow()
        {
            if (ConsoleRow.Height.Value > 0)
            {
                ConsoleRow.Height = new GridLength(0);
            }
            {
                ConsoleRow.Height = new GridLength(200);
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

            SetStatus("Saving ...");
            UwpAudio.Save(Song, folder);
            SetStatus($"Saved to {StoragePath}");
        }

        private void Load()
        {
        }

        private void ComboBpm_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var value = (int)ComboBpm.SelectedValue;
            if (!Song.Tracks.Any())
            {
                Song.BeatsPerMinute = value;
            }
            else
            {
                ComboBpm.SelectedItem = Song.BeatsPerMinute;
                Status.Text = "Can't set BPM on a song with tracks.";
            }
        }

        private void ComboBeatsPerBar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var value = (int)ComboBeatsPerBar.SelectedValue;
            if (!Song.Tracks.Any())
            {
                Song.BeatsPerBar = value;
            }
            else
            {
                ComboBeatsPerBar.SelectedItem = Song.BeatsPerBar;
                Status.Text = "Can't set beats per bar on a song with tracks.";
            }
        }
    }
}
