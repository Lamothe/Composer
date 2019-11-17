using Composer.Core.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Composer.Model
{
    public class UwpAudio : IAudio, IDisposable
    {
        private const int ElementSize = sizeof(float);

        private bool IsGraphStarted { get; set; }
        private AudioGraph Graph { get; set; }
        public DeviceInformationCollection OutputDevices { get; private set; }
        private AudioDeviceOutputNode OutputDevice { get; set; }
        private AudioDeviceInputNode InputDevice { get; set; }

        public event EventHandler Ready;
        public event EventHandler Started;
        public event EventHandler Stopped;
        public event EventHandler<Song> Playing;
        public event EventHandler<Track> Recording;

        public int SamplesPerSecond => (int)(Graph.EncodingProperties.Bitrate / Graph.EncodingProperties.BitsPerSample);

        public UwpAudio()
        {
            Create();
        }

        private async void Create()
        {
            OutputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());

            var settings = new AudioGraphSettings(AudioRenderCategory.Media)
            {
                QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
            };

            var audioGraphResult = await AudioGraph.CreateAsync(settings);
            if (audioGraphResult.Status != AudioGraphCreationStatus.Success)
            {
                throw new ApplicationException($"Audio graph error: {audioGraphResult.Status}");
            }
            Graph = audioGraphResult.Graph;

            InputDevice = await CreateInputDevice().ConfigureAwait(true);
            OutputDevice = await CreateOutputDevice().ConfigureAwait(true);

            Ready?.Invoke(this, EventArgs.Empty);
        }

        public async Task<AudioDeviceInputNode> CreateInputDevice()
        {
            var deviceInputNodeResult = await Graph.CreateDeviceInputNodeAsync(MediaCategory.Other);
            if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new ApplicationException($"Input device error: {deviceInputNodeResult.Status}");
            }
            return deviceInputNodeResult.DeviceInputNode;
        }

        public async Task<AudioDeviceOutputNode> CreateOutputDevice()
        {
            var deviceOutputNodeResult = await Graph.CreateDeviceOutputNodeAsync();
            if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new ApplicationException($"Output device error: {deviceOutputNodeResult.Status}");
            }
            return deviceOutputNodeResult.DeviceOutputNode;
        }

        public AudioFrameInputNode CreateFrameInputNode()
        {
            var encodingProperties = Graph.EncodingProperties;
            encodingProperties.ChannelCount = 1;
            var result = Graph.CreateFrameInputNode(encodingProperties);
            result.Stop();
            return result;
        }

        public AudioFrameOutputNode CreateFrameOutputNode()
        {
            var encodingProperties = Graph.EncodingProperties;
            encodingProperties.ChannelCount = 1;
            return Graph.CreateFrameOutputNode(encodingProperties);
        }

        public static MediaEncodingProfile CreateMediaEncodingProfile(StorageFile file)
        {
            switch (file.FileType.ToString().ToLowerInvariant())
            {
                case ".wma":
                    return MediaEncodingProfile.CreateWma(AudioEncodingQuality.High);
                case ".mp3":
                    return MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High);
                case ".wav":
                    return MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                default:
                    throw new ArgumentException();
            }
        }

        private void Start()
        {
            lock (Graph)
            {
                Graph.Start();
                IsGraphStarted = true;
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Stop()
        {
            lock (Graph)
            {
                if (IsGraphStarted)
                {
                    Graph.Stop();
                    IsGraphStarted = false;
                    Stopped?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private static unsafe float[] ReadSamplesFromFrame(AudioFrameOutputNode frameOutputNode)
        {
            using (var frame = frameOutputNode.GetFrame())
            {
                using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                {
                    using (var reference = buffer.CreateReference())
                    {
                        ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* unsafeBuffer, out uint numberOfBytes);

                        var numberOfSamples = (int)numberOfBytes / ElementSize;
                        if (numberOfSamples <= 0)
                        {
                            return null;
                        }

                        var samples = new float[numberOfSamples];

                        var length = Math.Min(numberOfSamples, samples.Length);
                        for (int i = 0; i < length; i++)
                        {
                            samples[i] = ((float*)unsafeBuffer)[i];
                        }

                        return samples;
                    }
                }
            }
        }

        private static unsafe AudioFrame GenerateFrameFromSamples(float[] samples)
        {
            var bufferSizeInBytes = samples.Length * ElementSize;

            var frame = new AudioFrame((uint)bufferSizeInBytes);
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);
                    var capacity = (int)capacityInBytes / ElementSize;
                    var dataInFloat = (float*)dataInBytes;
                    var length = Math.Min(samples.Length, capacity);
                    for (int i = 0; i < length; i++)
                    {
                        dataInFloat[i] = samples[i];
                    }
                }
            }

            return frame;
        }

        public void Dispose()
        {
            if (Graph != null)
            {
                Graph.Dispose();
            }
        }

        public void Record(Track track)
        {
            lock (Graph)
            {
                if (IsGraphStarted)
                {
                    throw new Exception("Graph is already running");
                }
            }

            var frameOutputNode = CreateFrameOutputNode();
            InputDevice.AddOutgoingConnection(frameOutputNode);

            void quantumStarted(AudioGraph s, object e)
            {
                var samples = ReadSamplesFromFrame(frameOutputNode);
                if (samples != null)
                {
                    if (!track.Write(samples, samples.Length))
                    {
                        Stop();
                    }
                }
            }

            void stopped(object sender, EventArgs e)
            {
                Graph.QuantumStarted -= quantumStarted;
                frameOutputNode.Dispose();
                Stopped -= stopped;
            };

            Stopped += stopped;
            Graph.QuantumStarted += quantumStarted;

            Start();
            Recording?.Invoke(this, track);
        }

        public async void Play(Song song, int position = 0)
        {
            if (song == null)
            {
                throw new ArgumentNullException(nameof(song));
            }

            song.SetPosition(position);

            lock (Graph)
            {
                if (IsGraphStarted)
                {
                    throw new Exception("Graph is already running");
                }
            }

            if (!song.Tracks.Any())
            {
                throw new Exception("Song does not have any tracks");
            }

            Graph.QuantumProcessed += (s, e) =>
            {
                var lastBarIndex = song.GetLastNonEmptyBarIndex() + 1;
                if (song.GetCurrentBar() >= lastBarIndex)
                {
                    Stop();
                }
            };

            foreach (var track in song.Tracks)
            {
                var input = CreateFrameInputNode();
                input.AddOutgoingConnection(OutputDevice);
                input.QuantumStarted += (inputNode, e) =>
                {
                    var samples = track.Read(track.Position, e.RequiredSamples);

                    if (samples != null)
                    {
                        using (var frame = GenerateFrameFromSamples(samples))
                        {
                            inputNode.AddFrame(frame);
                        }
                    }

                    track.Position += Graph.SamplesPerQuantum;
                };
                input.Start();
            }

            Start();
            Playing?.Invoke(this, song);
        }

        public static async void Save(Song song, StorageFolder folder, string fileName)
        {
            for (int trackIndex = 0; trackIndex < song.Tracks.Count(); trackIndex++)
            {
                var track = song.Tracks[trackIndex];
                var lastBarIndex = track.GetLastNonEmptyBarIndex();
                var file = await folder?.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                if (file == null)
                {
                    throw new Exception($"Failed to create file '{fileName}'");
                }

                for (int barIndex = 0; barIndex <= lastBarIndex; barIndex++)
                {
                    using (var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
                    {
                        var bar = track.Bars[lastBarIndex];
                        if (bar.Buffer != null)
                        {
                            var buffer = bar.Buffer.SelectMany(BitConverter.GetBytes).ToArray();
                            stream.Write(buffer, 0, bar.Track.Song.SamplesPerBar);
                            stream.Close();
                        }
                    }
                }
            }
        }

        public static async void Load(StorageFolder folder, Song song)
        {
            var trackIndex = 0;
            var files = await folder.GetFilesAsync();

            if (files.Any(x => x.Name.StartsWith($"bar-{trackIndex}-")))
            {
            }
        }

        public async void SaveTrackProper(Track track, StorageFile file)
        {
            var trackLengthInSamples = track.Song.SamplesPerBar * track.GetLastNonEmptyBarIndex();
            var samplesRead = 0;

            var fileProfile = CreateMediaEncodingProfile(file);
            var fileOutputNodeResult = await Graph.CreateFileOutputNodeAsync(file, fileProfile);
            if (fileOutputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                throw new Exception("Failed to create file output node");
            }
            var output = fileOutputNodeResult.FileOutputNode;

            var input = CreateFrameInputNode();
            input.AddOutgoingConnection(output);
            void quantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs e)
            {
                var samples = track.Read(samplesRead, e.RequiredSamples);
                samplesRead += samples.Length;
                using (var frame = GenerateFrameFromSamples(samples))
                {
                    input.AddFrame(frame);
                }
            }
            void quantumProcessed(AudioGraph sender, object o)
            {
                var position = (int)Graph.CompletedQuantumCount * Graph.SamplesPerQuantum;

                if (position >= trackLengthInSamples)
                {
                    Graph.QuantumProcessed -= quantumProcessed;
                    input.QuantumStarted -= quantumStarted;
                    Stop();
                }
            }
            input.QuantumStarted += quantumStarted;
            Graph.QuantumProcessed += quantumProcessed;

            input.Start();
        }
    }
}
