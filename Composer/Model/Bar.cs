using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Storage;

namespace Composer.Model
{
    public class Bar
    {
        public event EventHandler Update;

        public float[] Buffer { get; set; }
        public Track Track { get; set; }

        public void SetEmpty()
        {
            Buffer = null;
            Update?.Invoke(this, EventArgs.Empty);
        }

        public void Write(float[] buffer, int sourceOffset, int destinationOffset, int length)
        {
            if (Buffer == null)
            {
                Buffer = new float[Track.Song.SamplesPerBar];
            }

            Array.Copy(buffer, sourceOffset, Buffer, destinationOffset, length);

            Update?.Invoke(this, EventArgs.Empty);
        }
    }
}
