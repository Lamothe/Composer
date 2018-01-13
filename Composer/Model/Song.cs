﻿using System;
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
        public Status Status { get; set; } = Status.Stopped;

        public event EventHandler StatusChanged;

        public void AddTrack(Model.Track track)
        {
            Tracks.Add(track);
            track.StatusChanged += (s, e) => {
                if (track.Status == Status.Stopped)
                {
                    if (!Tracks.Any(t => t.Status != Status.Stopped))
                    {
                        ChangeStatus(Status.Stopped);
                    }
                }
            };
        }

        public void Record()
        {
            ChangeStatus(Status.Recording);
        }

        public void Play()
        {
            if (Status == Status.Stopped)
            {
                Tracks.ForEach(x => x.Play());
                ChangeStatus(Status.Playing);
            }
        }

        public void Stop()
        {
            if (Status != Status.Stopped)
            {
                Tracks.ForEach(x => x.Stop());
                ChangeStatus(Status.Stopped);
            }
        }

        public void ChangeStatus(Status status)
        {
            if (status != Status)
            {
                Status = status;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
