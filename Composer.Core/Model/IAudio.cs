using System;


namespace Composer.Core.Model
{
    public interface IAudio
    {
        event EventHandler Ready;
        event EventHandler<int> PositionUpdated;
        event EventHandler<Song> Playing;
        event EventHandler<Track> Recording;
        event EventHandler Stopped;

        int SamplesPerSecond { get; }

        int SampleRate { get; }

        void Record(Track track);

        void Play(Song song);

        void Stop();
    }
}
