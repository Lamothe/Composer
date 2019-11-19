using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Composer.Core.Model
{
    public class Bar
    {
        public event EventHandler<bool> Updated;

        public float[] Buffer { get; private set; }
        public int Length { get; private set; } = 0;

        public Track Track { get; private set; }

        public Bar(Track track)
        {
            Track = track;
            Buffer = new float[Track.Song.SamplesPerBar];
        }

        public void SetEmpty()
        {
            Buffer = new float[Track.Song.SamplesPerBar];
            Length = 0;
            Updated?.Invoke(this, true);
        }

        public void SetBuffer(float[] buffer)
        {
            if (buffer.Length > Buffer.Length)
            {
                throw new Exception("Buffer is too large");
            }
            buffer.CopyTo(Buffer, 0);
            Length = buffer.Length;
            Updated?.Invoke(this, true);
        }

        public void Write(float[] buffer, int sourceOffset, int destinationOffset, int length)
        {
            if (length > (Buffer.Length - destinationOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(length), $"Length is larger than remaining space in bar buffer");
            }

            if (length > (buffer.Length - sourceOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(length), $"Length is larger than buffer");
            }

            Array.Copy(buffer, sourceOffset, Buffer, destinationOffset, length);
            Length += length;
            Updated?.Invoke(this, false);
        }
    }
}
