using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARKRegionsEditor
{
    public class GameTranslationText
    {
        public string Text { get; set; }
    }

    public class GameTranslationChildren
    {
        public GameTranslationText Source { get; set; }
        public GameTranslationText Translation { get; set; }
    }

    public class GameTranslationNamespace
    {
        public string Namespace { get; set; }
        public List<GameTranslationChildren> Children { get; set; }
    }

    public class GameTranslation
    {
        public string FormatVersion { get; set; }
        public string Namespace { get; set; }
        public List<GameTranslationNamespace> Subnamespaces { get; set; }
    }
}
