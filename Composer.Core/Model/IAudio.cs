using System;


namespace Composer.Core.Model
{
    public interface IAudio
    {
        int SampleRate { get; }

        void Record(Track track);

        void Play(Song song);

        void Stop();
    }
}
