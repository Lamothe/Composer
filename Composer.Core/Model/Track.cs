using System;
using System.Collections.Generic;
using System.Linq;

namespace Composer.Core.Model
{
    public class Track
    {
        public Song Song { get; set; }
        public string Name { get; set; }
        public Guid Id { get; set; }
        public List<Bar> Bars { get; set; } = new List<Bar>();
        public bool IsMuted { get; set; }
        public int WritePosition { get; set; } = 0;

        public EventHandler<Bar> BarAdded;

        public int? GetBarIndexAtPosition(int position)
        {
            var barIndex = position / Song.SamplesPerBar;

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
            var totalBufferLength = Bars.Count() * Song.SamplesPerBar;
            var bar = GetBarAtPosition(position);

            if (bar == null)
            {
                return null;
            }

            if (IsMuted || bar.Buffer == null)
            {
                return new float[numberOfSamples];
            }

            var offset = position % Song.SamplesPerBar;

            var length = Math.Min(numberOfSamples, bar.Buffer.Length - offset);

            var buffer = new float[length];
            Array.Copy(bar.Buffer, offset, buffer, 0, length);
            return buffer;
        }

        public bool Write(float[] samples, int count)
        {
            var bar = GetBarAtPosition(WritePosition);

            if (bar == null)
            {
                bar = AddBar();
            }

            var offset = WritePosition % Song.SamplesPerBar;
            var remainingSpaceInBuffer = Song.SamplesPerBar - offset;
            var length = Math.Min(count, remainingSpaceInBuffer);

            bar.Write(samples, 0, offset, length);

            if (count > remainingSpaceInBuffer)
            {
                bar = GetBarAtPosition(WritePosition);

                if (bar == null)
                {
                    return false;
                }

                bar.Write(samples, length, 0, count - remainingSpaceInBuffer);
            }

            WritePosition += count;

            return true;
        }

        public Bar AddBar()
        {
            var bar = new Bar(this);
            Bars.Add(bar);
            BarAdded?.Invoke(this, bar);
            return bar;
        }
    }
}
