using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace Composer.Model
{
    public class Track
    {
        public string Name { get; set; }
        public List<Bar> Bars { get; set; } = new List<Bar>();
        public int SamplesPerBar { get; set; }
        public bool IsMuted { get; set; }

        public int? GetBarIndexAtPosition(int position)
        {
            var barIndex = position / SamplesPerBar;

            if (barIndex >= Bars.Count())
            {
                return null;
            }

            return barIndex;
        }

        public Bar GetBarAtPosition(int position)
        {
            var barIndex = GetBarIndexAtPosition(position);
            return !barIndex.HasValue ? null : Bars[barIndex.Value];
        }

        public int GetLastNonEmptyBarIndex()
        {
            int lastBarIndex = 0;

            for (int barIndex = 0; barIndex < Bars.Count(); barIndex++)
            {
                var bar = Bars[barIndex];
                if (bar.Buffer != null && barIndex > lastBarIndex)
                {
                    lastBarIndex = barIndex;
                }
            }

            return lastBarIndex;
        }

        public float[] Read(int position, int numberOfSamples)
        {
            var totalBufferLength = Bars.Count() * SamplesPerBar;
            var bar = GetBarAtPosition(position);

            if (bar == null)
            {
                return null;
            }

            if (IsMuted || bar.Buffer == null)
            {
                return new float[numberOfSamples];
            }

            var offset = position % SamplesPerBar;

            var length = Math.Min(numberOfSamples, bar.Buffer.Length - offset);

            var buffer = new float[length];
            Array.Copy(bar.Buffer, offset, buffer, 0, length);
            return buffer;
        }

        public bool Write(float[] samples, int position)
        {
            var bar = GetBarAtPosition(position);

            if (bar == null)
            {
                return false;
            }

            var offset = position % SamplesPerBar;
            var remainingSpaceInBuffer = SamplesPerBar - offset;
            var length = Math.Min(samples.Length, remainingSpaceInBuffer);

            bar.Write(samples, 0, offset, length);

            if (samples.Length > remainingSpaceInBuffer)
            {
                bar = GetBarAtPosition(position);

                if (bar == null)
                {
                    return false;
                }

                bar.Write(samples, length, 0, samples.Length - remainingSpaceInBuffer);
            }

            return true;
        }
    }
}
