﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Composer.Model
{
    public class Song
    {
        public int BeatsPerMinute { get; set; } = 120;
        public int BeatsPerBar { get; set; } = 4;
        public List<Track> Tracks { get; set; } = new List<Track>();

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
