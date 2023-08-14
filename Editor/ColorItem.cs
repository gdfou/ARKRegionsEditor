using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ARKRegionsEditor
{
    public class ColorItem
    {
        public ColorItem(string name, string label, Color color) { Name = name; Label = label; Color = color; }
        public string Name { get; set; }
        public string Label { get; set; }
        public Color Color { get; set; }
    }
}
