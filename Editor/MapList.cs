using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using static ARKRegionsEditor.ArkWiki;

namespace ARKRegionsEditor
{
    public class MapListJsonMapBorder
    {
        public int? width { get; set; }
        public string color { get; set; }
    }

    public class MapListJsonItem
    {
        public string name { get; set; }
        public string map { get; set; }
        public string mapFile { get; set; }
        public ArkWikiJsonRect coordinateBorders { get; set; }
        public List<List<float>> at { get; set; }
        public MapListJsonMapBorder map_border { get; set; }
        public string regions { get; set; }

        public string regionsFile;

        public override string ToString()
        {
            return name;
        }
    }

    public class MapListJson
    {
        public List<string> list { get; set; }
        public List<MapListJsonItem> maps { get; set; }

        static public MapListJson LoadFromResource(string jsonResName)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(jsonResName);
            using StreamReader reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<MapListJson>(reader.ReadToEnd(), new JsonSerializerOptions {ReadCommentHandling=JsonCommentHandling.Skip});
        }
    }
}
