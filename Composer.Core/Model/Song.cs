using System;
using System.Collections.Generic;
using System.Linq;

namespace Composer.Core.Model
{
    public class Song
    {
        public int BeatsPerMinute { get; set; } = 90;
        public int BeatsPerBar { get; set; } = 4;
        public int SamplesPerBar { get; set; }
        public List<Track> Tracks { get; set; } = new List<Track>();
        public int? BeginLoop { get; set; }
        public int? EndLoop { get; set; }

        public EventHandler<Track> TrackAdded;

        public Track CreateTrack(string name)
        {
            var track = new Track
            {
                Song = this,
                Name = name
            };
            Tracks.Add(track);
            TrackAdded?.Invoke(this, track);
            return track;
        }

        public void AddTrack(Model.Track track)
        {
            Tracks.Add(track);
        }

        public int GetLastNonEmptyBarIndex()
        {
            return Tracks.Max(track => track.GetLastNonEmptyBarIndex());
        }
    }
}
