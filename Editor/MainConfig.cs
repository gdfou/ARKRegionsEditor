using System;
using System.Collections.Generic;

namespace ARKRegionsEditor
{
    public class JsonRect
    {
        public int left { get; set; }
        public int top { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
    public class MainConfig
    {
        public JsonRect window { get; set; }
        public string splitter_pos { get; set; }
        public string obelisk_path { get; set; }
        public string translate_path { get; set; }
        public Dictionary<string, MapListJsonItem> maps { get; set; }
    }
}
