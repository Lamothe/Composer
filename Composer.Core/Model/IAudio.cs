using System;


namespace Composer.Core.Model
{
    public interface IAudio
    {
        event EventHandler Ready;
        event EventHandler<AudioStatus> AudioStatusChanged;

        int SamplesPerSecond { get; }

        void Record(Track track); 

        void Play(Song song, int position = 0);

        void Stop();
    }
}
