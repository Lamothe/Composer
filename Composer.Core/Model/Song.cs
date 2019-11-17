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
        public int Position { get; private set; }

        public EventHandler<Track> TrackAdded;
        public EventHandler<Track> TrackRemoved;
        public EventHandler<Song> PositionUpdated;

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
            track.PositionUpdated += (s, e) => CalculatePosition();
            return track;
        }

        public void RemoveTrack(Track track)
        {
            track.Clear();
            Tracks.Remove(track);
            TrackRemoved?.Invoke(this, track);
        }

        public int GetLastNonEmptyBarIndex()
        {
            return Tracks.Max(track => track.GetLastNonEmptyBarIndex());
        }

        public void CalculatePosition()
        {
            var position = Tracks.Min(x => x.Position);
            if (position != Position)
            {
                Position = position;
                PositionUpdated?.Invoke(this, this);
            }
        }

        public void SetPosition(int position)
        {
            Tracks.ForEach(x => x.Position = position);
        }

        public int GetCurrentBar()
        {
            return Position / SamplesPerBar;
        }

        public decimal GetTime()
        {
            var bars = Position / (decimal)SamplesPerBar;
            var secondsPerBar = 60 * BeatsPerBar / (decimal)BeatsPerMinute;
            return bars * secondsPerBar;
        }
    }
}
