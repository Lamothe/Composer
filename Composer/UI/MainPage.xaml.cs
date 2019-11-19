using Composer.Model;
using System;
using System.Collections.Concurrent;
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
    public sealed partial class MainPage : Page, IDisposable
    {
        private string StoragePath = null;
        private Core.Model.Song Song { get; set; }
        private int TrackSequence { get; set; } = 0;
        private UI.Bar SelectedBar { get; set; } = null;
        public Core.Model.AudioStatus AudioStatus { get; set; }
        public decimal SecondsPerBar { get; set; }
        private Core.Model.IAudio Audio { get; set; }
        private ConcurrentQueue<UI.Bar> UpdateBars { get; set; } = new ConcurrentQueue<UI.Bar>();
        private System.Timers.Timer Timer { get; set; }
        private bool UpdatePosition { get; set; }

        public MainPage()
        {
            this.InitializeComponent();

            SetStatus("Initialising ...");

            App.Current.UnhandledException += (s, e) => AddLog(e.Message);

            Background = Constants.ApplicationBackgroundBrush;
            StatusBar.Background = Constants.StatusBarBackgroundBrush;
            StatusBar.BorderBrush = Constants.StatusBarBorderBrush;
            ConsoleRow.Height = new GridLength(0);

            InitialiseKeyboardAccelerators();
            CreateSong();

            Timer = new System.Timers.Timer(100);
            Timer.Elapsed += (s, e) =>
            {
                while (UpdateBars.Any())
                {
                    UpdateBars.TryDequeue(out UI.Bar bar);
                    UI.Utilities.CallUI(() => bar?.Update());
                }

                if (UpdatePosition)
                {
                    UI.Utilities.CallUI(() => Position.Text = Song.GetTime().ToTimeString());
                    UpdatePosition = false;
                }
            };
            Timer.Start();

            Audio = new UwpAudio();
            Audio.Ready += (s, e) => OnAudioReady();
        }

        private void CreateSong()
        {
            Song = new Core.Model.Song();
            Song.TrackAdded += (sender, track) => OnTrackAdded(track);
            Song.TrackRemoved += (sender, track) => OnTrackRemoved(track);
            Song.PositionUpdated += (s, position) => UpdatePosition = true;
        }

        private void InitialiseKeyboardAccelerators()
        {
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
                        SelectedBar.Model.SetBuffer(new float[Song.SamplesPerBar]);
                    }

                    var content = Clipboard.GetContent();
                    if (content.AvailableFormats.Contains("PCM"))
                    {
                        var data = await content.GetDataAsync("PCM");
                        SelectedBar.Model.SetBuffer(data as float[]);
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
                    SelectedBar.Update();
                }
            };
            KeyboardAccelerators.Add(delete);

            var left = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Left };
            left.Invoked += (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null)
                {
                    int column = Grid.GetColumn(SelectedBar);
                    int row = Grid.GetRow(SelectedBar);
                    if (row >= 0 && column >= 0)
                    {
                        var selected = BarGrid.GetChildAt<UI.Bar>(row, column - 1);
                        if (selected != null)
                        {
                            selected.Select();
                        }
                    }
                }
            };
            KeyboardAccelerators.Add(left);

            var right = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Right };
            right.Invoked += (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null)
                {
                    int column = Grid.GetColumn(SelectedBar);
                    int row = Grid.GetRow(SelectedBar);
                    if (row >= 0 && column >= 0)
                    {
                        var selected = BarGrid.GetChildAt<UI.Bar>(row, column + 1);
                        if (selected != null)
                        {
                            selected.Select();
                        }
                    }
                }
            };
            KeyboardAccelerators.Add(right);

            var up = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Up };
            up.Invoked += (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null)
                {
                    int column = Grid.GetColumn(SelectedBar);
                    int row = Grid.GetRow(SelectedBar);
                    if (row >= 0 && column >= 0)
                    {
                        var selected = BarGrid.GetChildAt<UI.Bar>(row - 1, column);
                        if (selected != null)
                        {
                            selected.Select();
                        }
                    }
                }
            };
            KeyboardAccelerators.Add(up);

            var down = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Down };
            down.Invoked += (s, e) =>
            {
                e.Handled = true;
                if (SelectedBar != null)
                {
                    int column = Grid.GetColumn(SelectedBar);
                    int row = Grid.GetRow(SelectedBar);
                    if (row >= 0 && column >= 0)
                    {
                        var selected = BarGrid.GetChildAt<UI.Bar>(row + 1, column);
                        if (selected != null)
                        {
                            selected.Select();
                        }
                    }
                }
            };
            KeyboardAccelerators.Add(down);

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
        }

        private void AddLog(string message)
        {
            UI.Utilities.CallUI(() => Log.Text += message + Environment.NewLine);
        }

        private void SetStatus(string message)
        {
            Status.Text = message;
            AddLog(message);
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

        private void OnAudioReady()
        {
            try
            {
                Audio.AudioStatusChanged += (sender, audioMode) => OnAudioStatusChanged(audioMode);

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

        private void OnTrackRemoved(Core.Model.Track track)
        {
            Stop();
            var index = TrackHeaders.GetChildren<UI.Track>().Select(x => x.Model).ToList().IndexOf(track);
            TrackHeaders.Children.RemoveAt(index);
            BarGrid.RowDefinitions.RemoveAt(0);

            TrackHeaders.GetChildren<UI.Track>(t => Grid.GetRow(t) > index).ForEach(t => Grid.SetRow(t, Grid.GetRow(t) - 1));
            BarGrid.GetChildren<UI.Bar>(b => Grid.GetRow(b) > index).ForEach(b => Grid.SetRow(b, Grid.GetRow(b) - 1));

            AddLog("Track removed");
        }

        private void OnTrackAdded(Core.Model.Track track)
        {
            track.BarAdded += (sender, bar) => UI.Utilities.CallUI(() => BarAdded(bar));
            track.BarRemoved += (sender, bar) => UI.Utilities.CallUI(() => BarRemoved(bar));
            track.Name = GenerateTrackName();
            var numberOfRows = BarGrid.RowDefinitions.Count();

            TrackHeaders.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Constants.TrackHeight) });
            BarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Constants.TrackHeight) });

            var ui = new UI.Track(track);
            ui.Deleted += (s, t) => Song.RemoveTrack(track);
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

            while (BarGrid.ColumnDefinitions.Count <= column)
            {
                BarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Constants.BarWidth) });
            }

            var ui = new UI.Bar(bar, track);
            ui.Selected += (s, b) =>
            {
                SelectedBar?.Deselect();
                SelectedBar = ui;
            };
            bar.Updated += (s, fullUpdate) =>
            {
                ui.FullUpdate = fullUpdate;
                UpdateBars.Enqueue(ui);
            };
            Grid.SetRow(ui, row);
            Grid.SetColumn(ui, column);

            BarGrid.Children.Add(ui);

            AddLog("Bar added");
        }

        private void BarRemoved(Core.Model.Bar bar)
        {
            var ui = BarGrid.GetChildren<UI.Bar>().Single(b => b.Model == bar);
            BarGrid.Children.Remove(ui);
            AddLog("Bar removed");
        }

        private void OnAudioStatusChanged(Core.Model.AudioStatus status)
        {
            SetAudioStatus(status);
        }

        private void OnPlaying(Core.Model.Song song)
        {
            SetAudioStatus(Core.Model.AudioStatus.Playing);
        }

        private void OnRecording(Core.Model.Track track)
        {
            SetAudioStatus(Core.Model.AudioStatus.Recording);
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
            if (AudioStatus == Core.Model.AudioStatus.Stopped)
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
            if (AudioStatus == Core.Model.AudioStatus.Stopped)
            {
                Audio.Record(Song);
            }
        }

        private void Play()
        {
            if (AudioStatus == Core.Model.AudioStatus.Stopped)
            {
                if (Song.Tracks.Any())
                {
                    Audio.Play(Song);
                }
            }
        }

        private void Stop()
        {
            if (AudioStatus != Core.Model.AudioStatus.Stopped)
            {
                Audio.Stop();
            }
        }

        private void SetAudioStatus(Core.Model.AudioStatus status)
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
            else
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
            UwpAudio.Save(Song, folder, "temp.cmp");
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

        public void Dispose()
        {
            Timer?.Dispose();
        }
    }
}
