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
        public Track Track { get; private set; }

        public Bar(Track track)
        {
            Track = track;
        }

        public void EmitUpdate()
        {
            Update?.Invoke(this, EventArgs.Empty);
        }

        public void SetEmpty()
        {
            Buffer = null;
            EmitUpdate();
        }

        public unsafe void Write(float* buffer, int sourceOffset, int destinationOffset, int length)
        {
            if (Buffer == null)
            {
                Buffer = new float[Track.SamplesPerBar];
            }

            for (int i = 0; i < length; i++)
            {
                Buffer[destinationOffset + i] = buffer[sourceOffset + i];
            }
        }
    }
}
