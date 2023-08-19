using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Navigation;

namespace ARKRegionsEditor
{
    public class Region : INotifyPropertyChanged
    {
        protected int priority_;
        protected bool error_;
        protected bool warning_;
        protected Visibility visibility_;
        protected bool editLabel_;
        protected string label_;

        public string Name { get; set; }

        public string Label
        {
            get => label_;
            set
            {
                if (label_ != value)
                {
                    label_ = value;
                    OnPropertyChanged("Label");
                }
            }
        }
        public string NewLabel { get; set; }

        public int Priority 
        {
            get => priority_;
            set
            {
                if (priority_ != value)
                {
                    priority_ = value;
                    OnPropertyChanged("Priority");
                }
            }
        }
        
        public bool Warning 
        {
            get => warning_;
            set
            {
                if (warning_ != value)
                {
                    warning_ = value;
                    OnPropertyChanged("Warning");
                }
            }
        }

        public bool Error 
        {
            get => error_;
            set
            {
                if(error_ != value)
                {
                    error_ = value;
                    OnPropertyChanged("Error");
                }
            }
        }

        public bool EditLabel 
        {
            get => editLabel_;
            set
            {
                if (editLabel_ != value)
                {
                    editLabel_ = value;

                    visibility_ = editLabel_ ? Visibility.Collapsed : Visibility.Visible;

                    LabelVisibilityVisu = visibility_;
                    LabelVisibilityEdit = visibility_;
                }
            }
        }

        public Visibility LabelVisibilityVisu
        {
            get => visibility_;
            set
            {
                OnPropertyChanged("LabelVisibilityVisu");
            }
        }

        public Visibility LabelVisibilityEdit
        {
            get => visibility_ == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            set
            {
                OnPropertyChanged("LabelVisibilityEdit");
            }
        }

        public List<MapZone> zones;

        public int ZoneCount 
        { 
            get => zones.Count;
        }

        public Region()
        {
            visibility_ = Visibility.Visible;
            zones = new List<MapZone>();
        }

        // Pour création d'une nouvelle région
        public Region(string newRegion, bool createDefaultZone) : this()
        {
            Name = newRegion;
            Label = newRegion;
            if (createDefaultZone)
            {
                zones.Add(new MapZone()
                {
                    LatLength = 5,
                    LonLength = 5
                });
            }
        }

        public Region(ObeliskJsonBiome biome) : this()
        {
            Name = biome.name;
            Label = biome.name;
            Priority = biome.priority;
        }

        public override string ToString()
        {
            return Label;
        }

        public void Update()
        {
            OnPropertyChanged("ZoneCount");
            OnPropertyChanged("Priority");
            foreach (var zone in zones)
            {
                zone.Update();
            }
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
