using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Composer.Core.Model
{
    public class Bar
    {
        public event EventHandler Updated;

        public float[] Buffer { get; set; }

        public Track Track { get; private set; }

        public Bar(Track track)
        {
            Track = track;
        }

        public void SetEmpty()
        {
            Buffer = null;
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public void SetBuffer(float[] buffer)
        {
            buffer?.CopyTo(Buffer, 0);
        }

        public void Write(float[] buffer, int sourceOffset, int destinationOffset, int length)
        {
            if (Buffer == null)
            {
                Buffer = new float[Track.Song.SamplesPerBar];
            }

            Array.Copy(buffer, sourceOffset, Buffer, destinationOffset, length);

            Updated?.Invoke(this, EventArgs.Empty);
        }
    }
}
