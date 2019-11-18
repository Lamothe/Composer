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
        public int Position { get; private set; } = 0;

        public EventHandler<Bar> BarAdded;
        public EventHandler<Bar> BarRemoved;
        public EventHandler<Track> PositionUpdated;

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

        public float[] Read(int numberOfSamples)
        {
            var bar = GetBarAtPosition(Position);

            if (bar == null)
            {
                return null;
            }

            if (IsMuted || bar.Buffer == null)
            {
                return new float[numberOfSamples];
            }

            var offset = Position % Song.SamplesPerBar;
            var length = Math.Min(numberOfSamples, bar.Buffer.Length - offset);

            var buffer = new float[length];
            Array.Copy(bar.Buffer, offset, buffer, 0, length);
            SetPosition(Position + length);
            return buffer;
        }

        public bool Write(float[] samples, int count)
        {
            var bar = GetBarAtPosition(Position);

            if (bar == null)
            {
                bar = AddBar();
            }

            var offset = Position % Song.SamplesPerBar;
            var remainingSpaceInBuffer = Song.SamplesPerBar - offset;
            var length = Math.Min(count, remainingSpaceInBuffer);

            bar.Write(samples, 0, offset, length);

            if (count > remainingSpaceInBuffer)
            {
                bar = GetBarAtPosition(Position);

                if (bar == null)
                {
                    return false;
                }

                bar.Write(samples, length, 0, count - remainingSpaceInBuffer);
            }

            SetPosition(Position + count);

            return true;
        }

        public void SetPosition(int position)
        {
            if (Position != position)
            {
                Position = position;
                PositionUpdated?.Invoke(this, this);
            }
        }

        public Bar AddBar()
        {
            var bar = new Bar(this);
            Bars.Add(bar);
            BarAdded?.Invoke(this, bar);
            return bar;
        }

        public void RemoveBar(Bar bar)
        {
            var index = Bars.IndexOf(bar);
            bar.SetEmpty();
            Bars.Remove(bar);
            BarRemoved?.Invoke(this, bar);
        }

        public void Clear()
        {
            while (Bars.Count > 0)
            {
                RemoveBar(Bars[0]);
            }
        }
    }
}
