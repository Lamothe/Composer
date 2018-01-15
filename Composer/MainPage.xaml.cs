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
        public int SamplesPerBar { get; set; }
        private Audio RecordingAudio { get; set; }
        private Audio PlaybackAudio { get; set; }
        private object PositionLocker = new object();

        public event EventHandler AudioStatusChanged;
        public event EventHandler PositionChanged;

        private async void CallUI(DispatchedHandler f) =>
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, f);


        public MainPage()
        {
            this.InitializeComponent();

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
                        var samplesPerBar = SelectedBar.Track.Model.SamplesPerBar;
                        SelectedBar.Model.Buffer = new float[samplesPerBar];
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
                        Tracks.Children.ToList().ForEach(x => (x as UI.Track).Update());
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
                var audio = await Audio.Create();
                var secondsPerBar = 60 * Song.BeatsPerBar / Song.BeatsPerMinute;
                SamplesPerBar = audio.SamplesPerSecond * secondsPerBar;

                PlayButton.IsEnabled = true;
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = true;
                MetronomeButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Status.Text = $"Exception: {ex.Message}";
            }
        }

        private void UpdateStatus()
        {
            var bars = Position / (decimal)SamplesPerBar;
            var secondsPerBar = 60 * Song.BeatsPerBar / (decimal)Song.BeatsPerMinute;
            var seconds = (decimal)(bars * secondsPerBar);
            Status.Text = $"{AudioStatus.ToString()}: {seconds.ToTimeString()} s";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
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
            var model = new Track
            {
                Name = GenerateTrackName(),
                SamplesPerBar = SamplesPerBar
            };
            Song.AddTrack(model);

            var ui = new UI.Track { Model = model };

            ui.DeleteTrack += (s, e) =>
            {
                Song.Tracks.Remove(ui.Model);
                Tracks.Children.Remove(ui);
                int row = 0;
                Tracks.ForEach<UI.Track>(t => Grid.SetRow(t, row++));
            };

            ui.ScrollViewChanged += (s, offset) =>
            {
                if (!ScrollUpdating)
                {
                    ScrollUpdating = true;
                    Tracks.ForEach<UI.Track>(x => x.UpdateScroll(offset));
                    ScrollUpdating = false;
                }
            };

            Tracks.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            Grid.SetRow(ui, Tracks.Children.Count());
            Grid.SetColumn(ui, 0);
            Tracks.Children.Add(ui);

            for (int count = 0; count < 100; count++)
            {
                var barModel = new Bar { SamplesPerBar = SamplesPerBar };
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
            Play();
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

        private async void Record()
        {
            if (AudioStatus == Model.Status.Stopped && RecordingAudio == null && PlaybackAudio == null)
            {
                SetPosition(0);

                var track = AddTrack();
                track.IsRecording = true;
                RecordingAudio = await Audio.Create();
                var input = await RecordingAudio.CreateInputDevice();
                var output = RecordingAudio.CreateFrameOutputNode();
                input.AddOutgoingConnection(output);
                RecordingAudio.Graph.QuantumStarted += (g, e) =>
                {
                    var samples = Audio.ReadSamplesFromFrame(output);
                    if (samples != null)
                    {
                        if (!track.Model.Write(samples, Position))
                        {
                            Stop();
                        }

                        SetPosition(Position + samples.Length);
                    }
                };
                RecordingAudio.Start();

                SetAudioStatus(Model.Status.Recording);
            }
        }

        private async void Play()
        {
            if (AudioStatus == Model.Status.Stopped && RecordingAudio == null && PlaybackAudio == null)
            {
                if (Song.Tracks.Any())
                {
                    SetPosition(0);

                    var lastBarIndex = Song.GetLastNonEmptyBarIndex() + 1;
                    PlaybackAudio = await Audio.Create();
                    PlaybackAudio.Graph.QuantumProcessed += (s, e) =>
                    {
                        SetPosition((int)PlaybackAudio.Graph.CompletedQuantumCount * PlaybackAudio.Graph.SamplesPerQuantum);

                        if (Position / SamplesPerBar >= lastBarIndex)
                        {
                            Stop();
                        }
                    };

                    var output = await PlaybackAudio.CreateOutputDevice();
                    foreach (var track in Song.Tracks)
                    {
                        var input = PlaybackAudio.CreateFrameInputNode();
                        input.AddOutgoingConnection(output);
                        input.QuantumStarted += (g, e) =>
                        {
                            var samples = track.Read(Position, e.RequiredSamples);
                            using (var frame = Audio.GenerateFrameFromSamples(samples))
                            {
                                input.AddFrame(frame);
                            }
                        };
                        input.Start();
                    }
                    PlaybackAudio.Start();
                    SetAudioStatus(Model.Status.Playing);
                }
            }
        }

        private void Stop()
        {
            if (RecordingAudio != null)
            {
                Tracks.ForEach<UI.Track>(x => x.IsRecording = false);
                RecordingAudio.Stop();
                RecordingAudio.Dispose();
                RecordingAudio = null;
            }

            if (PlaybackAudio != null)
            {
                PlaybackAudio.Stop();
                PlaybackAudio.Dispose();
                PlaybackAudio = null;
            }

            SetAudioStatus(Model.Status.Stopped);
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

            var lastBarIndex = Song.GetLastNonEmptyBarIndex() + 1;
            var saveAudio = await Audio.Create();
            var position = 0;
            var trackIndex = 0;

            foreach (var track in Song.Tracks)
            {
                var trackFile = await folder.CreateFileAsync($"track{++trackIndex}.mp3", CreationCollisionOption.ReplaceExisting);
                var fileProfile = Audio.CreateMediaEncodingProfile(trackFile);
                var fileOutputNodeResult = await saveAudio.Graph.CreateFileOutputNodeAsync(trackFile, fileProfile);
                if (fileOutputNodeResult.Status != Windows.Media.Audio.AudioFileNodeCreationStatus.Success)
                {
                    throw new Exception("Failed to create file output node");
                }
                var output = fileOutputNodeResult.FileOutputNode;
                var input = saveAudio.CreateFrameInputNode();
                input.AddOutgoingConnection(output);
                input.QuantumStarted += (g, e) =>
                {
                    var samples = track.Read(position, e.RequiredSamples);
                    using (var frame = Audio.GenerateFrameFromSamples(samples))
                    {
                        input.AddFrame(frame);
                    }
                };
                input.Start();
            }

            var locker = new object();
            saveAudio.Graph.QuantumProcessed += (s, e) =>
            {
                lock (locker)
                {
                    if (saveAudio != null)
                    {
                        position = (int)saveAudio.Graph.CompletedQuantumCount * saveAudio.Graph.SamplesPerQuantum;

                        if (position / SamplesPerBar >= lastBarIndex)
                        {
                            saveAudio.Stop();
                            saveAudio.Dispose();
                            saveAudio = null;

                            CallUI(() => Status.Text = $"Song saved to {StoragePath}");
                        }
                    }
                }
            };

            saveAudio.Graph.Start();
        }
    }
}
