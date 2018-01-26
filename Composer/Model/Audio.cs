using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public class Audio : IDisposable
    {
        public AudioGraph Graph { get; private set; }
        public DeviceInformationCollection OutputDevices { get; private set; }
        public AudioDeviceInputNode DeviceInputNode { get; private set; }
        public AudioDeviceOutputNode DeviceOutputNode { get; private set; }
        private const int ElementSize = sizeof(float);
        public event EventHandler Stopped;
        public event EventHandler<int> PositionUpdated;
        public event EventHandler Completed;

        public int SamplesPerSecond => (int)(Graph.EncodingProperties.Bitrate / Graph.EncodingProperties.BitsPerSample);

        private Audio()
        {
        }

        public static async Task<Audio> Create()
        {
            var audio = new Audio
            {
                OutputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector())
            };

            var settings = new AudioGraphSettings(AudioRenderCategory.Media)
            {
                QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
            };

            var audioGraphResult = await AudioGraph.CreateAsync(settings);
            if (audioGraphResult.Status != AudioGraphCreationStatus.Success)
            {
                throw new ApplicationException($"Audio graph error: {audioGraphResult.Status}");
            }
            audio.Graph = audioGraphResult.Graph;

            return audio;
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

        public void Start()
        {
            Graph.Start();
        }

        public void Stop()
        {
            Graph.Stop();
            Graph.ResetAllNodes();
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        public void Reset()
        {
            Graph.ResetAllNodes();
        }

        public static unsafe float[] ReadSamplesFromFrame(AudioFrameOutputNode frameOutputNode)
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

        public static unsafe AudioFrame GenerateFrameFromSamples(float[] samples)
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

        public async void Record(Track track)
        {
            var input = await CreateInputDevice();
            var output = CreateFrameOutputNode();
            input.AddOutgoingConnection(output);

            var position = 0;
            void quantumStarted(AudioGraph s, object e)
            {
                var samples = ReadSamplesFromFrame(output);
                if (samples != null)
                {
                    if (!track.Write(samples, position))
                    {
                        Stop();
                        Completed?.Invoke(this, EventArgs.Empty);
                    }

                    position += samples.Length;

                    PositionUpdated?.Invoke(this, position);
                }
            }

            void stopped(object sender, EventArgs e)
            {
                input.Dispose();
                output.Dispose();
                Graph.QuantumStarted -= quantumStarted;
                Stopped -= stopped;
            };

            Stopped += stopped;
            Graph.QuantumStarted += quantumStarted;

            Start();
        }

        public async void Play(Song song)
        {
            if (!song.Tracks.Any())
            {
                throw new Exception("Song does not have any tracks");
            }

            var position = 0;
            var output = await CreateOutputDevice();
            var lastBarIndex = song.GetLastNonEmptyBarIndex() + 1;

            void quantumProcessed(AudioGraph sender, object o)
            {
                position = (int)Graph.CompletedQuantumCount * Graph.SamplesPerQuantum;
                PositionUpdated?.Invoke(this, position);

                if (position / song.Tracks.First().SamplesPerBar >= lastBarIndex)
                {
                    Stop();
                    Completed?.Invoke(this, EventArgs.Empty);
                }
            }

            Graph.QuantumProcessed += quantumProcessed;

            void stopped(object sender, EventArgs e)
            {
                Graph.QuantumProcessed -= quantumProcessed;
                output.Dispose();
                Stopped -= stopped;
            };

            Stopped += stopped;

            foreach (var track in song.Tracks)
            {
                var input = CreateFrameInputNode();
                input.AddOutgoingConnection(output);
                void quantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs e)
                {
                    var samples = track.Read(position, e.RequiredSamples);
                    using (var frame = GenerateFrameFromSamples(samples))
                    {
                        input.AddFrame(frame);
                    }
                };
                void inputStopped(object sender, EventArgs e)
                {
                    input.QuantumStarted -= quantumStarted;
                    input.Dispose();
                    Stopped -= inputStopped;
                }
                input.QuantumStarted += quantumStarted;
                Stopped += inputStopped;
                input.Start();
            }

            Start();
        }

        public static async void Save(Song song, StorageFolder folder)
        {
            for (int trackIndex = 0; trackIndex <= song.Tracks.Count(); trackIndex++)
            {
                var track = song.Tracks[trackIndex];
                var lastBarIndex = track.GetLastNonEmptyBarIndex();
                for (int barIndex = 0; barIndex <= lastBarIndex; barIndex++)
                {
                    var fileName = $"bar-{trackIndex}-{barIndex}.pcm";
                    var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                    using (var stream = await file.OpenStreamForWriteAsync())
                    {
                        var bar = track.Bars[lastBarIndex];
                        if (bar.Buffer != null)
                        {
                            var buffer = bar.Buffer.SelectMany(BitConverter.GetBytes).ToArray();
                            stream.Write(buffer, 0, bar.SamplesPerBar);
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
            var trackLengthInSamples = track.SamplesPerBar * track.GetLastNonEmptyBarIndex();
            var samplesRead = 0;

            var fileProfile = Audio.CreateMediaEncodingProfile(file);
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
                using (var frame = Audio.GenerateFrameFromSamples(samples))
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
                    Completed?.Invoke(this, EventArgs.Empty);
                }
            }
            input.QuantumStarted += quantumStarted;
            Graph.QuantumProcessed += quantumProcessed;

            input.Start();
            Start();
        }
    }
}
