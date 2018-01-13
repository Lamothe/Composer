using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;

namespace Composer.Model
{
    public class Track
    {
        private const int ElementSize = sizeof(float);

        public AudioFrameInputNode FrameInputNode { get; set; }
        public AudioFrameOutputNode FrameOutputNode { get; set; }

        public Status Status { get; set; } = Status.Stopped;

        public string Name { get; set; }
        public uint Position { get; set; } = 0;
        public ConcurrentList<Bar> Bars { get; set; } = new ConcurrentList<Bar>();
        public IEnumerator<Bar> BarEnumerator { get; set; }
        private Bar WriteBar { get; set; }
        public uint SamplesPerBar { get; set; }
        public bool IsMuted { get; set; }

        public event EventHandler StatusChanged;

        public delegate void BarEventHandler(object sender, Model.Bar bar);
        public event BarEventHandler BarAdded;

        public Track(string name, Audio audio)
        {
            var encodingProperties = audio.Graph.EncodingProperties;
            encodingProperties.ChannelCount = 1;

            Name = name;
            FrameInputNode = audio.Graph.CreateFrameInputNode(encodingProperties);
            FrameOutputNode = audio.Graph.CreateFrameOutputNode(encodingProperties);

            audio.DeviceInputNode.AddOutgoingConnection(FrameOutputNode);
            FrameInputNode.AddOutgoingConnection(audio.DeviceOutputNode);

            audio.Graph.QuantumStarted += (g, e) => Write();
            FrameInputNode.QuantumStarted += (g, e) => Read(e.RequiredSamples);
        }

        public unsafe void Write()
        {
            using (var frame = FrameOutputNode.GetFrame())
            {
                if (Status == Status.Recording)
                {
                    using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                    {
                        using (var reference = buffer.CreateReference())
                        {
                            ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* unsafeBuffer, out uint numberOfBytes);
                            var numberOfFloats = numberOfBytes / ElementSize;

                            if (WriteBar == null || Position + numberOfFloats >= WriteBar.Buffer.Length)
                            {
                                Position = 0;
                                WriteBar = new Bar
                                {
                                    Buffer = new float[SamplesPerBar]
                                };
                                Bars.Add(WriteBar);
                                BarAdded?.Invoke(this, WriteBar);
                            }

                            for (int i = 0; i < numberOfFloats; i++)
                            {
                                WriteBar.Buffer[Position + i] = ((float*)unsafeBuffer)[i];
                            }

                            Position += numberOfFloats;

                            WriteBar.EmitUpdate();
                        }
                    }
                }
            }
        }

        public unsafe void Read(int numberOfSamples)
        {
            if (Status == Status.Playing)
            {
                if (numberOfSamples > 0)
                {
                    var readBar = BarEnumerator.Current;
                    var bufferRemaining = readBar.Buffer.Length - Position;

                    if (bufferRemaining <= 0)
                    {
                        if (!BarEnumerator.MoveNext())
                        {
                            Stop();
                            return;
                        }

                        Position = 0;

                        readBar = BarEnumerator.Current;
                        bufferRemaining = readBar.Buffer.Length - Position;

                        if (bufferRemaining <= 0)
                        {
                            Stop();
                            return;
                        }
                    }

                    var bufferSize = (uint)Math.Min(numberOfSamples, bufferRemaining);
                    var bufferSizeInBytes = bufferSize * ElementSize;

                    using (var frame = new AudioFrame(bufferSizeInBytes))
                    {
                        using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                        {
                            using (var reference = buffer.CreateReference())
                            {
                                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);
                                var capacity = capacityInBytes / ElementSize;
                                bufferSize = (uint)Math.Min(bufferSize, capacity);
                                var dataInFloat = (float*)dataInBytes;
                                for (int i = 0; i < bufferSize; i++)
                                {
                                    if (IsMuted)
                                    {
                                        dataInFloat[i] = 0;
                                    }
                                    else
                                    {
                                        dataInFloat[i] = readBar.Buffer[i + (uint)Position];
                                    }
                                }
                            }
                        }

                        FrameInputNode.AddFrame(frame);
                    }

                    Position += bufferSize;
                }
            }
        }

        public void Record()
        {
            if (Status == Status.Stopped)
            {
                FrameOutputNode.Start();
                ChangeStatus(Status.Recording);
            }
        }

        public void Play()
        {
            if (Status == Status.Stopped)
            {
                Position = 0;
                BarEnumerator = Bars.GetEnumerator();
                if (BarEnumerator.MoveNext())
                {
                    FrameInputNode.Start();
                    ChangeStatus(Status.Playing);
                }
            }
        }

        public void Stop()
        {
            FrameInputNode.Stop();
            FrameOutputNode.Stop();
            ChangeStatus(Status.Stopped);
        }

        private void ChangeStatus(Status status)
        {
            Status = status;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
