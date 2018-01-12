﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Composer.UI
{
    public sealed partial class Track : UserControl
    {
        public bool IsSelected { get; set; }
        public Model.Track Model { get; set; }
        public uint BarWidth { get; set; } = 200;

        public event EventHandler DeleteTrack;

        public Track()
        {
            this.InitializeComponent();

            DeleteButton.Click += (s, e) => DeleteTrack?.Invoke(this, EventArgs.Empty);
        }

        public void Update()
        {
            foreach (var ui in Bars.Children)
            {
                (ui as UI.Bar).Update();
            }

            Info.Text = $"{Model.Name}\r\n{Model.Status}";
        }

        public UI.Bar AddBar(Model.Bar model)
        {
            var ui = new UI.Bar
            {
                Model = model,
                Width = BarWidth
            };

            model.Update += (s, e) => ui.QueueUpdate();

            Bars.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(BarWidth)
            });

            Grid.SetRow(ui, 0);
            Grid.SetColumn(ui, Bars.Children.Count());
            Bars.Children.Add(ui);

            return ui;
        }
    }
}
