
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
        private AudioFrameInputNode frameInputNode = null;

        public int BeatsPerMinute { get; set; }
        private const int Frequecy = 440;
        private const float Amplitude = 0.3f;
        private uint SampleRate;
        private float SampleIncrement;
        private float Theta;

        public Metronome(Audio audio)
        {
            var graph = audio.Graph;

            SampleRate = graph.EncodingProperties.SampleRate;
            SampleIncrement = (float)(Frequecy * (Math.PI * 2)) / SampleRate;

            frameInputNode = graph.CreateFrameInputNode();
            frameInputNode.QuantumStarted += (g, e) =>
            {
                if (e.RequiredSamples > 0)
                {
                    frameInputNode.AddFrame(Read(e.RequiredSamples));
                }
            };

            frameInputNode.Stop();
            frameInputNode.AddOutgoingConnection(audio.DeviceOutputNode);
        }
        
        public unsafe AudioFrame Read(int numberOfSamples)
        {
            if (numberOfSamples == 0)
            {
                throw new ArgumentException("Cannot request zero samples", nameof(numberOfSamples));
            }

            var bufferSizeInBytes = (uint)numberOfSamples * ElementSize;

            var frame = new AudioFrame(bufferSizeInBytes);

            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);
                    var capacity = capacityInBytes / ElementSize;
                    var dataInFloat = (float*)dataInBytes;

                    for (int i = 0; i < numberOfSamples; i++)
                    {
                        double sinValue = Amplitude * Math.Sin(Theta);
                        dataInFloat[i] = (float)sinValue;
                        Theta += SampleIncrement;
                    }
                }
            }

            return frame;
        }

        public void Play()
        {
            frameInputNode.Start();
        }

        public void Stop()
        {
            frameInputNode.Stop();
        }
    }
}
