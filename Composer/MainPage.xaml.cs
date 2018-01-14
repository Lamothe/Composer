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
        private Model.Audio Audio { get; set; }
        private Model.Metronome Metronome { get; set; }
        private bool Updated { get; set; } = false;
        private int TrackSequence { get; set; } = 0;
        private bool ScrollUpdating = false;
        private int QuantumsProcessed = 0;
        private UI.Bar SelectedBar = null;
        private double Zoom = 0;

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

            Song.StatusChanged += (s, e) => Updated = true;
            Song.PositionChanged += (s, e) => Updated = true;

            Tracks.PointerWheelChanged += (s, e) =>
            {
                var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

                if ((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0)
                {
                    Zoom += delta * 0.01;
                }
            };

            var timer = new System.Timers.Timer(200);
            timer.Elapsed += async (s, e) =>
            {
                if (Updated)
                {
                    Updated = false;
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateStatus();
                        Tracks.Children.ToList().ForEach(x => (x as UI.Track).Update());
                    });
                }
            };
            timer.Start();
        }

        private void UpdateStatus()
        {
            var seconds = Song.Position / (decimal)Audio.SamplesPerSecond;
            if (Song.Status == Model.Status.Stopped)
            {
                Stop();
            }

            Status.Text = $"{Song.Status.ToString()}: {seconds.ToTimeString()} s";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private async void Load()
        {
            try
            {
                Audio = await Model.Audio.Create();
                Metronome = new Model.Metronome(Audio);

                PlayButton.IsEnabled = true;
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = true;
                MetronomeButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
            }
            catch (Exception)
            {
                // Ignore access is denied
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

        private object QuantumProcessedLocker = new object();
        private UI.Track AddTrack()
        {
            var samplesPerMinute = 60 * Audio.SamplesPerSecond;

            var model = new Track(GenerateTrackName(), Audio);
            model.SamplesPerBar = (samplesPerMinute * Song.BeatsPerBar) / Song.BeatsPerMinute;
            Audio.Graph.QuantumStarted += (g, e) =>
            {
                model.Write(Song.Position);
            };
            model.FrameInputNode.QuantumStarted += (g, e) =>
            {
                model.Read(Song.Position, e.RequiredSamples);
            };
            Audio.Graph.QuantumProcessed += (g, e) =>
            {
                var unprocessedQuantums = (int)g.CompletedQuantumCount - QuantumsProcessed;

                lock (QuantumProcessedLocker)
                {
                    if (unprocessedQuantums >= 0)
                    {
                        QuantumsProcessed = (int)g.CompletedQuantumCount;
                    }
                }

                Song.IncrementPosition(unprocessedQuantums * g.SamplesPerQuantum);
            };
            model.StatusChanged += (s, e) => Updated = true;
            Song.AddTrack(model);

            var ui = new UI.Track { Model = model };

            ui.DeleteTrack += (s, e) =>
            {
                ui.Model.Stop();
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

            model.BarAdded += (s, bar) =>
            {
                Updated = true;
                bar.Update += (s1, e) => Updated = true;

                var barUI = ui.AddBar(bar);
                barUI.PointerPressed += (s1, e) =>
                {
                    barUI.Select(true);
                };
                barUI.Selected += (s1, e) =>
                {
                    SelectedBar?.Select(false);
                    SelectedBar = barUI;
                };
                barUI.Deleted += (s1, e) =>
                {
                    if (SelectedBar == barUI)
                    {
                        SelectedBar = null;
                    }
                };
            };

            for (int count = 0; count < 100; count++)
            {
                model.AddBar();
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

        private void ToggleRecord()
        {
            if (Song.Status == Model.Status.Stopped)
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
            if (Song.Status == Model.Status.Stopped)
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
            if (Song.Status == Model.Status.Stopped)
            {
                var track = AddTrack();
                track.Model.Record();

                Song.Record();
                Audio.Start();
            }
        }

        private void Play()
        {
            if (Song.Tracks.Any())
            {
                Song.Play();
                Audio.Start();
            }
        }

        private void Stop()
        {
            Song.Stop();
            Audio.Stop();
        }

        private async void Save()
        {
            StorageFolder folder = null;

            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");
            folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            StoragePath = folder.Name;

            int trackIndex = 0;
            foreach (var track in Song.Tracks)
            {
                var trackFile = await folder.CreateFileAsync($"track{++trackIndex}.mp3", CreationCollisionOption.ReplaceExisting);
                var fileProfile = Audio.CreateMediaEncodingProfile(trackFile);
                var fileOutputNodeResult = await Audio.Graph.CreateFileOutputNodeAsync(trackFile, fileProfile);
                track.FrameInputNode.AddOutgoingConnection(fileOutputNodeResult.FileOutputNode);
                track.Play();
                Audio.Start();
                await fileOutputNodeResult.FileOutputNode.FinalizeAsync();
            }

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }
    }
}
