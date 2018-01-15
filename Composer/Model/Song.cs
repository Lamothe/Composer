using System;
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
            int lastBarIndex = 0;

            foreach (var track in Tracks)
            {
                for (int barIndex = 0; barIndex < track.Bars.Count(); barIndex++)
                {
                    var bar = track.Bars[barIndex];
                    if (bar.Buffer != null && barIndex > lastBarIndex)
                    {
                        lastBarIndex = barIndex;
                    }
                }
            }

            return lastBarIndex;
        }
    }
}
