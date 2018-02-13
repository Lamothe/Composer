using Android.App;
using Android.Widget;
using Android.OS;
using Composer.Core.Model;
using Composer.AndroidUI.Model;
using Android.Content.PM;
using Android.Views;

namespace Composer.AndroidUI
{
    [Activity(Label = "Composer.Android", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        public const int NumberOfBarsPerTrack = 13;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);

            SetStatus("Initialising ...");

            var audio = new LowLevelAudio();
            audio.OnMessage += (s, m) => SetStatus(m);
            var song = new Song { SamplesPerBar = audio.SampleRate };

            var buttonRecord = FindViewById<Button>(Resource.Id.buttonRecord);
            var buttonStop = FindViewById<Button>(Resource.Id.buttonStop);
            var buttonPlay = FindViewById<Button>(Resource.Id.buttonPlay);

            var index = 0;
            buttonRecord.Click += (s, e) => audio.Record(song.CreateTrack($"Track {++index}"));
            buttonStop.Click += (s, e) => audio.Stop();
            buttonPlay.Click += (s, e) => audio.Play(song);
            song.TrackAdded += (s, t) => AddTrack(t);

            SetStatus("Ready");
        }

        private void SetStatus(string message)
        {
            RunOnUiThread(() => {
                var textViewStatus = FindViewById<TextView>(Resource.Id.textViewStatus);
                textViewStatus.Text = message;
            });
        }

        private class BarView : LinearLayout
        {
            public Bar Bar { get; private set; }

            public BarView(Android.Content.Context context, Bar bar, int index) : base(context)
            {
                Bar = bar;
                AddView(new Button(context) { Text = $"Bar {index + 1}" });
                SetMinimumHeight(100);
                SetMinimumWidth(200);
            }
        }

        private void AddTrack(Track track)
        {
            var tracksUI = FindViewById<LinearLayout>(Resource.Id.linearLayoutTracks);

            var trackUI = new LinearLayout(BaseContext) { Orientation = Orientation.Horizontal };
            var barsUI = new LinearLayout(BaseContext) { Orientation = Orientation.Horizontal };
            var trackLabel = new Button(BaseContext) { Text = track.Name };
            trackLabel.SetMinimumHeight(100);
            trackLabel.SetMinimumWidth(200);
            trackUI.AddView(trackLabel);
            trackUI.AddView(barsUI);
            tracksUI.AddView(trackUI);

            for (var i = 0; i < NumberOfBarsPerTrack; i++)
            {
                var bar = new Bar(track);
                track.Bars.Add(bar);
                var barUI = new BarView(BaseContext, bar, i);
                barsUI.AddView(barUI);
            }
        }
    }
}

