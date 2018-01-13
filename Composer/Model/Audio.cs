using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
    public class Audio
    {
        public AudioGraph Graph { get; private set; }
        public DeviceInformationCollection OutputDevices { get; private set; }
        public AudioDeviceInputNode DeviceInputNode { get; private set; }
        public AudioDeviceOutputNode DeviceOutputNode { get; private set; }

        public int SamplesPerSecond => (int)(Graph.EncodingProperties.Bitrate / Graph.EncodingProperties.BitsPerSample);

        private Audio()
        {
        }

        public static async Task<Audio> Create()
        {
            var audio = new Audio();

            audio.OutputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());

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

            var deviceOutputNodeResult = await audio.Graph.CreateDeviceOutputNodeAsync();
            if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new ApplicationException($"Output device error: {deviceOutputNodeResult.Status}");
            }
            audio.DeviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

            var deviceInputNodeResult = await audio.Graph.CreateDeviceInputNodeAsync(MediaCategory.Other);
            if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new ApplicationException($"Input device error: {deviceInputNodeResult.Status}");
            }
            audio.DeviceInputNode = deviceInputNodeResult.DeviceInputNode;

            audio.Graph.Start();

            return audio;
        }

        private static MediaEncodingProfile CreateMediaEncodingProfile(StorageFile file)
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
    }
}
