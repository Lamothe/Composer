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

        private AudioGraph Graph { get; set; }
        public DeviceInformationCollection OutputDevices { get; private set; }
        private AudioDeviceOutputNode OutputDevice { get; set; }
        private AudioDeviceInputNode InputDevice { get; set; }
        private AudioFrameOutputNode RecordingOutputNode { get; set; }
        private AudioStatus Status { get; set; }
        private bool IsGraphStarted => Status == AudioStatus.Playing || Status == AudioStatus.Recording;
        private Song Song { get; set; }
        private Track RecordingTrack { get; set; }
        private List<AudioFrameInputNode> InputNodes { get; set; } = new List<AudioFrameInputNode>();

        public event EventHandler Ready;
        public event EventHandler<AudioStatus> AudioStatusChanged;

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
            Graph.QuantumProcessed += (audioGraph, e) => AudioGraphQuantumProcessed();
            Graph.QuantumStarted += (audioGraph, e) => AudioGraphQuantumStarted();

            InputDevice = await CreateInputDevice().ConfigureAwait(true);
            OutputDevice = await CreateOutputDevice().ConfigureAwait(true);
            RecordingOutputNode = CreateFrameOutputNode();

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

        private void SetAudioStatus(AudioStatus status)
        {
            lock (Graph)
            {
                if (status != Status)
                {
                    if (status != AudioStatus.Stopped && Status != AudioStatus.Stopped)
                    {
                        throw new Exception($"Can't transition from {Status} to {status}");
                    }

                    if (status == AudioStatus.Recording)
                    {
                        InputDevice.AddOutgoingConnection(RecordingOutputNode);
                        InputDevice.Start();
                        Graph.Start();
                    }
                    else if (status == AudioStatus.Playing)
                    {
                        Graph.Start();
                    }
                    else if (status == AudioStatus.Stopped)
                    {
                        Graph.Stop();

                        if (Status == AudioStatus.Recording)
                        {
                            InputDevice.RemoveOutgoingConnection(RecordingOutputNode);
                            InputDevice.Stop();
                        }
                        else if (Status == AudioStatus.Playing)
                        {
                            InputNodes.ForEach(x =>
                            {
                                x.RemoveOutgoingConnection(OutputDevice);
                                x.Dispose();
                            });
                            InputNodes.Clear();
                        }
                    }

                    Status = status;
                    AudioStatusChanged?.Invoke(this, status);
                }
            }
        }

        public void Stop()
        {
            SetAudioStatus(AudioStatus.Stopped);
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

        public void Record(Song song)
        {
            if (song == null)
            {
                throw new ArgumentNullException(nameof(song));
            }

            lock (Graph)
            {
                if (IsGraphStarted)
                {
                    throw new Exception("Graph is already running");
                }
            }

            Song = song;
            var track = song.AddTrack();
            RecordingTrack = track;
            Song.SetPosition(0);

            SetAudioStatus(AudioStatus.Recording);
        }

        private void AudioGraphQuantumStarted()
        {
            if (Status == AudioStatus.Recording)
            {
                var samples = ReadSamplesFromFrame(RecordingOutputNode);
                if (samples != null)
                {
                    RecordingTrack.Write(samples, samples.Length);
                }
            }
        }

        private void AudioGraphQuantumProcessed()
        {
        }

        private void InputNodeQuantumStarted(AudioFrameInputNode inputNode, FrameInputNodeQuantumStartedEventArgs e, Track track)
        {
            if (Status == AudioStatus.Playing)
            {
                var samples = track.Read(e.RequiredSamples);

                if (samples != null)
                {
                    using (var frame = GenerateFrameFromSamples(samples))
                    {
                        inputNode.AddFrame(frame);
                    }
                }
            }
        }

        public async void Play(Song song, int position = 0)
        {
            if (song == null)
            {
                throw new ArgumentNullException(nameof(song));
            }

            Song = song;

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

            foreach (var track in song.Tracks)
            {
                var inputNode = CreateFrameInputNode();
                inputNode.QuantumStarted += (i, e) => InputNodeQuantumStarted(i, e, track);
                InputNodes.Add(inputNode);
                inputNode.AddOutgoingConnection(OutputDevice);
                inputNode.Start();
            }

            SetAudioStatus(AudioStatus.Playing);
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
    }
}
