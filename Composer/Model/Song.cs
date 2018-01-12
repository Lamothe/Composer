using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Composer.Model
{
    public class Song
    {
        public uint BeatsPerMinute { get; set; } = 120;
        public uint BeatsPerBar { get; set; } = 4;
        public List<Track> Tracks { get; set; } = new List<Track>();
    }
}
