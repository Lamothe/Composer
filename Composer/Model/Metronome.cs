using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;

namespace Composer.Model
{
    public class Metronome
    {
        private const int ElementSize = sizeof(float);

        private const int Frequency = 440;
        private const float Amplitude = 0.3f;
        private const float Duration = 0.1f;

        public int BeatsPerMinute { get; set; }
        private uint SampleRate { get; set; }
        private float SampleIncrement { get; set; }
        private AudioDeviceOutputNode OutputDevice { get; set; }
        private int TotalSampleCount { get; set; }
        public EventHandler<string> Info { get; set; }

        public unsafe AudioFrame Read(int numberOfSamples)
        {
            if (numberOfSamples == 0)
            {
                throw new ArgumentException("Cannot request zero samples", nameof(numberOfSamples));
            }

            var bufferSizeInBytes = (uint)numberOfSamples * ElementSize;
            var timeInSeconds = TotalSampleCount / (float)SampleRate;

            var frame = new AudioFrame(bufferSizeInBytes);
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);
                    var capacity = capacityInBytes / ElementSize;
                    var dataInFloat = (float*)dataInBytes;

                    var beatsPerSeconds = BeatsPerMinute / 60.0;
                    var offset = timeInSeconds % (1 / beatsPerSeconds);

                    if (offset < Duration)
                    {
                        for (int i = 0; i < numberOfSamples; i++)
                        {
                            var sampleIndex = TotalSampleCount + i;
                            dataInFloat[i] = (float)(Amplitude * Math.Sin((sampleIndex / (float)SampleRate * Frequency)));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numberOfSamples; i++)
                        {
                            dataInFloat[i] = 0;
                        }
                    }

                    Info?.Invoke(this, $"BPM: {BeatsPerMinute} BPS: {beatsPerSeconds:0.00} Offset: {offset:0.00} time: {timeInSeconds:0.00}");
                }
            }

            TotalSampleCount += numberOfSamples;

            return frame;
        }

        public async void Play(Audio audio, int beatsPerMinute)
        {
            TotalSampleCount = 0;

            var graph = audio.Graph;

            SampleRate = graph.EncodingProperties.SampleRate;
            BeatsPerMinute = beatsPerMinute;

            if (OutputDevice == null)
            {
                OutputDevice = await audio.CreateOutputDevice();

                var frameInputNode = graph.CreateFrameInputNode();
                frameInputNode.QuantumStarted += (g, e) =>
                {
                    if (e.RequiredSamples > 0)
                    {
                        using (var frame = Read(e.RequiredSamples))
                        {
                            frameInputNode.AddFrame(frame);
                        }
                    }
                };

                frameInputNode.AddOutgoingConnection(OutputDevice);
            }

            graph.Start();
        }
    }
}
