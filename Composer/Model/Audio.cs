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
    }
}
