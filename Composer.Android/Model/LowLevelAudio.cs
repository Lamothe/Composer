using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Composer.Core.Model;
using Java.IO;

namespace Composer.AndroidUI.Model
{
    public class LowLevelAudio : IAudio
    {
        public int SampleRate { get { return 44100; } }
        public int MetronomeFrequency { get; private set; }  = 440;

        private AudioRecord recorder = null;
        private AudioTrack player = null;
        private Thread recordingThread = null;
        private const int BufferSize = 100;
        private object stateLocker = new object();
        private const int ReadBlocking = 0;
        public EventHandler<string> OnMessage;
        private float[] MetronomeSamples { get; set; }

        public LowLevelAudio()
        {
            GenerateMetronomeSamples();
        }

        public void SetMetronomeFrequency(int frequency)
        {
            MetronomeFrequency = frequency;
            GenerateMetronomeSamples();
        }

        private void GenerateMetronomeSamples()
        {
            if (MetronomeSamples == null)
            {
                var scale = 2 * Math.PI * MetronomeFrequency / (float)SampleRate;

                var metronomeBufferSize = 5512; // That's 0.25 seconds, 110 cycles  
                MetronomeSamples = Enumerable.Range(0, metronomeBufferSize)
                    .Select(x => (float)Math.Sin(x * scale))
                    .ToArray();
            }
        }

        public void Play(Song song)
        {
            lock (stateLocker)
            {
                player = new AudioTrack(Stream.Music, SampleRate, ChannelOut.Mono, Encoding.PcmFloat, 1000, AudioTrackMode.Stream);

                player.Play();

                OnMessage?.Invoke(this, "Playing");

                var done = false;
                var position = 0;

                while (!done)
                {
                    var accumulator = new float[BufferSize];
                    var numberOfMergedTracks = 0;
                    var numberOfMergedSamples = 0;

                    foreach (var track in song.Tracks)
                    {
                        var samples = track.Read(position, BufferSize);
                        if (samples != null)
                        {
                            numberOfMergedSamples = Math.Min(samples.Length, accumulator.Length);
                            for (int i = 0; i < numberOfMergedSamples; i++)
                            {
                                accumulator[i] += samples[i];
                            }
                            numberOfMergedTracks++;
                        }
                    }

                    if (numberOfMergedTracks == 0)
                    {
                        break;
                    }

                    var mergedSamples = accumulator
                        .Take(numberOfMergedSamples)
                        .Select(x => x / (float)numberOfMergedTracks)
                        .ToArray();

                    var result = player.Write(mergedSamples, 0, mergedSamples.Length, WriteMode.Blocking);

                    if (result < 0)
                    {
                        throw new Exception($"Failed read: {result}");
                    }

                    position += result;
                }

                Stop();
            }
        }

        public void Record(Track track)
        {
            lock (stateLocker)
            {
                recorder = new AudioRecord(AudioSource.Mic, SampleRate, ChannelIn.Mono, Encoding.PcmFloat, BufferSize * sizeof(float));

                if (recorder.State != State.Initialized)
                {
                    throw new Exception("Failed to initialise audio recorder.");
                }

                recorder.StartRecording();
                recordingThread = new Thread(() => Write(track));
                recordingThread.Start();
                
                OnMessage?.Invoke(this, "Recording");
            }
        }
       
        private void Write(Track track)
        {
            var monoSampleRate = SampleRate / 2;
            player = new AudioTrack(Stream.Music, SampleRate, ChannelOut.Mono,
                Encoding.PcmFloat, MetronomeSamples.Length * sizeof(float), AudioTrackMode.Stream);
            player.Play();

            var beatIntervalInSeconds = 60 / (float)track.Song.BeatsPerMinute;
            var beatIntervalInSamples = (int)(monoSampleRate * beatIntervalInSeconds);

            var nextBeat = beatIntervalInSamples;
            var buffer = new float[BufferSize];
            while (recorder != null && recorder.RecordingState == RecordState.Recording)
            {
                int numberOfSamples = recorder.Read(buffer, 0, BufferSize, ReadBlocking);

                if (numberOfSamples < 0)
                {
                    throw new Exception("Failed to read");
                }

                if (track.WritePosition >= nextBeat)
                {
                    nextBeat += beatIntervalInSamples;
                    player.Write(MetronomeSamples, 0, MetronomeSamples.Length, WriteMode.NonBlocking);
                }

                if (!track.Write(buffer, numberOfSamples))
                {
                    break;
                }
            }

            Stop();
        }

        public void Stop()
        {
            lock (stateLocker)
            {
                if (recorder != null)
                {
                    if (recorder.RecordingState == RecordState.Recording)
                    {
                        recorder.Stop();
                        recorder.Release();
                        recorder = null;
                        recordingThread = null;
                    }
                }

                if (player != null)
                {
                    if (player.PlayState == PlayState.Playing)
                    {
                        player.Stop();
                        player.Release();
                        player = null;
                    }
                }

                OnMessage?.Invoke(this, "Stopped");
            }
        }
    }
}