using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using static ARKRegionsEditor.ArkWiki;

namespace ARKRegionsEditor
{
    public class MapZone : INotifyPropertyChanged
    {
        public float Lat { get; set; }
        public float Lon { get; set; }
        public float LatLength { get; set; }
        public float LonLength { get; set; }
        public bool Inside { get; set; }

        public MapPos Center
        {
            get
            {
                return new MapPos(Lat + LatLength/2, Lon + LonLength/2);
            }
        }

        public MapPos Pos
        {
            get
            {
                return new MapPos(Lat, Lon);
            }
        }

        public bool Warning 
        {
            get
            {
                return (LonLength < 2 || LatLength < 2);
            }
        }

        public bool Error => false; // for Listview binding compatibility

        public MapZone()
        {
        }

        public MapZone(ObeliskJsonBiomeBoxes boxes, ArkWikiJsonRect borders)
        {
            var start = new MapPos(boxes.start, borders);
            var end = new MapPos(boxes.end, borders);
            Lat = start.lat;
            Lon = start.lon;
            LatLength = (float)Math.Round(end.lat - start.lat);
            if (LatLength == 0)
            {
                LatLength = 1;
            }
            LonLength = (float)Math.Round(end.lon - start.lon);
            if (LonLength == 0)
            {
                LonLength = 1;
            }
        }

        public MapZone(Rect rect)
        {
            Lat = (float)rect.Top;
            Lon = (float)rect.Left;
            LatLength = (float)rect.Height;
            LonLength = (float)rect.Width;
        }

        public void CopyFrom(MapZone src)
        {
            this.Lat = src.Lat;
            this.Lon = src.Lon;
            this.LatLength = src.LatLength;
            this.LonLength = src.LonLength;
            this.Inside = src.Inside;
        }

        public bool Equals(MapZone src)
        {
            return src.Lat == this.Lat && src.Lon == this.Lon && src.LatLength == this.LatLength && src.LonLength == this.LonLength;
        }

        private Rect ToRectangle()
        {
            return new System.Windows.Rect(Lon, Lat, LonLength, LatLength);
        }

        public bool Contains(MapZone zone)
        {
            var rzone = zone.ToRectangle();
            var rthis = this.ToRectangle();
            return rthis.Contains(rzone);
        }

        public bool IntersectsWith(MapZone zone)
        {
            var rzone = zone.ToRectangle();
            var rthis = this.ToRectangle();
            return rthis.IntersectsWith(rzone);
        }

        public static MapZone Intersect(MapZone zone1, MapZone zone2)
        {
            var rzone1 = zone1.ToRectangle();
            var rzone2 = zone2.ToRectangle();
            return new MapZone(Rect.Intersect(rzone1, rzone2));
        }

        public override string ToString()
        {
            return $"({Lat};{Lon});({LatLength};{LonLength})";
        }

        public void Update()
        {
            OnPropertyChanged("Lat");
            OnPropertyChanged("Lon");
            OnPropertyChanged("LatLength");
            OnPropertyChanged("LonLength");
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class Region : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public int Priority { get; set; }
        public bool Warning { get; set; }
        public bool Error { get; set; }
        public List<MapZone> zones;

        public int ZoneCount 
        { 
            get => zones.Count;
        }

        public Region()
        {
            zones = new List<MapZone>();
        }

        public Region(ObeliskJsonBiome biome)
        {
            Name = biome.name;
            Label = biome.name;
            Priority = biome.priority;
            zones = new List<MapZone>();
        }

        public override string ToString()
        {
            return Label;
        }

        public void Update()
        {
            OnPropertyChanged("ZoneCount");
            OnPropertyChanged("Warning");
            OnPropertyChanged("Error");
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
