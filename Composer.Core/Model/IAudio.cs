using System;


namespace Composer.Core.Model
{
    public interface IAudio
    {
        event EventHandler Completed;
        event EventHandler<int> PositionUpdated;

        int SamplesPerSecond { get; }

        int SampleRate { get; }

        void Record(Track track);

        void Play(Song song);

        void Stop();
    }
}
