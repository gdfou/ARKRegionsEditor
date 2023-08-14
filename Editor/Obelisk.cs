using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARKRegionsEditor
{
    public class ObeliskJsonBiomePoint
    {
        public float lat { get; set; }
        [JsonPropertyName("long")]
        public float lon { get; set; }
    }

    public class ObeliskJsonBiomeBoxes
    {
        public ObeliskJsonBiomePoint start { get; set; }
        public ObeliskJsonBiomePoint center { get; set; }
        public ObeliskJsonBiomePoint end { get; set; }
    }

    public class ObeliskJsonBiome
    {
        public string name { get; set; }
        public int priority { get; set; }
        public bool isOutside { get; set; }
        public bool preventCrops { get; set; }
        //public temperature
        //public wind
        public List<ObeliskJsonBiomeBoxes> boxes { get; set; }
    }

    public class ObeliskJsonBiomes
    {
        //[JsonPropertyName("$schema")]
        //public string schema { get; set; }
        public string version { get; set; }
        //public core
        public string format { get; set; }
        public string persistentLevel { get; set; }
        public List<ObeliskJsonBiome> biomes { get; set; }
    }
}
