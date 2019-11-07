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
        public EventHandler<Track> TrackRemoved;

        public Track AddTrack(string name = "Untitled")
        {
            var track = new Track
            {
                Song = this,
                Name = name,
                Id = Guid.NewGuid()
            };
            Tracks.Add(track);
            TrackAdded?.Invoke(this, track);
            return track;
        }

        public void RemoveTrack(Track track)
        {
            track.Bars.Clear();
            Tracks.Remove(track);
            TrackRemoved?.Invoke(this, track);
        }

        public int GetLastNonEmptyBarIndex()
        {
            return Tracks.Max(track => track.GetLastNonEmptyBarIndex());
        }
    }
}
