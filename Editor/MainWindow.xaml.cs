using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using static ARKRegionsEditor.ArkWiki;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Text.Encodings.Web;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using System.Windows.Documents;

namespace ARKRegionsEditor
{
    /// <summary>
    /// Main Windows
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        int lockInterface_;
        MainConfig cfg_;
        MapListJson mapList_;
        Dictionary<string, string> translation_;
        Dictionary<string, string> reserveTranslation_;
        ObservableCollection<Region> regionsList_;
        ObservableCollection<Region> biomesList_;
        ObservableCollection<MapZone> zonesList_;
        ObservableCollection<ColorItem> colorList_;
        ArkWikiJsonRegions jsonRegions_;
        GridViewColumnHeader listViewRegionsSortCol_;
        SortAdorner listViewRegionsSortAdorner_;
        SortDescription listViewRegionsSortDescription_;
        int saveCounter_;
        int lockKeyboard_;

        const string ShooterGame_archive = @"C:\Program Files (x86)\Steam\steamapps\common\ARK\ShooterGame\Content\Localization\Game\fr\ShooterGame.archive";

        public MainWindow()
        {
            translation_ = new Dictionary<string, string>();
            reserveTranslation_ = new Dictionary<string, string>();
            regionsList_ = new ObservableCollection<Region>();
            zonesList_ = new ObservableCollection<MapZone>();
            biomesList_ = new ObservableCollection<Region>();
            DataContext = this;
            InitializeComponent();

            LoadMainConfig();

            colorList_ = new ObservableCollection<ColorItem>
            {
                new ColorItem("Black", "Noir", Colors.Black),
                new ColorItem("White", "Blanc", Colors.White),
                new ColorItem("Blue", "Bleu", Colors.Blue),
                new ColorItem("Red", "Rouge", Colors.Red),
                new ColorItem("Green", "Vert", Colors.Green),
                new ColorItem("Yellow", "Jaune", Colors.Yellow),
                new ColorItem("Magenta", "Magenta", Colors.Magenta),
                new ColorItem("Brown", "Marron", Colors.Brown),
                new ColorItem("Navy", "Bleu Marine", Colors.Navy),
                new ColorItem("Olive", "Olive", Colors.Olive),
                new ColorItem("Salmon", "Saumon", Colors.Salmon)
            };
            comboBoxZonesColor.ItemsSource = colorList_;

            if (cfg_ == null)
            {
                cfg_ = new MainConfig()
                {
                    window = new JsonRect(),
                    obelisk_path = @"<drive>:<obelisk_folder>\data\wiki\",
                    translate_path = ShooterGame_archive
                };
            }
        }

        /// <summary>
        ///     Event raised when the Window has loaded.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lockInterface_++;

            // Load map list
            mapList_ = MapListJson.LoadFromResource("ARKRegionsEditor.Ressources.MapList.json");
            foreach (var map in mapList_.list)
            {
                if (map == "-")
                {
                    comboBoxMap.Items.Add(new Separator());
                }
                else
                {
                    var map_def = mapList_.maps.Find(x => x.name == map);
                    comboBoxMap.Items.Add(map_def);
                    if (map_def.coordinateBorders == null)
                    {
                        map_def.coordinateBorders = new ArkWikiJsonRect() { left = 0, top = 0, bottom = 100, right = 100 };
                    }
                }
            }

            if (cfg_.maps != null)
            {
                foreach (var map in cfg_.maps)
                {
                    var map_def = mapList_.maps.Find(x => x.name == map.Key);
                    if (map_def != null)
                    {
                        map_def.regionsFile = map.Value.regions;
                    }
                }
            }

            mapViewer.ZoomInFull();
            mapViewer.MaxScale = 8;

            mapViewer.CommandEvent += MapViewer_CommandEvent;

            checkboxViewRegion.IsChecked = mapViewer.RegionVisibility;
            checkboxViewZone.IsChecked = mapViewer.ZonesVisibility;

            listviewRegions.ItemsSource = regionsList_;
            listviewBiomes.ItemsSource = biomesList_;
            listviewZones.ItemsSource = zonesList_;

            comboBoxZonesColor.SelectedItem = colorList_.First(x => x.Color == mapViewer.ZonesColor);
            sliderZonesOpacity.Value = mapViewer.ZonesOpacity;
            if (cfg_.translate_path == null)
            {
                cfg_.translate_path = ShooterGame_archive;
            }
            LoadTranslations(cfg_.translate_path);

            listviewRegionsColumnName.Width = 0;
            labelInfo.Text = "Choisissez une carte, puis chargez les régions ou les biomes.";

            lockInterface_--;
        }

        private void MapViewer_CommandEvent(object sender, CommandEventArgs e)
        {
            switch(e.Command)
            {
                case "EnterEditMode":
                    {
                        tabItemRegions.IsEnabled = false;
                        tabItemBiomes.IsEnabled = false;
                        tabControlMap.Tag = tabControlMap.SelectedItem;
                        tabControlMap.SelectedItem = tabItemZones;
                        labelInfo.Text = "mode édition";
                        break;
                    }

                case "ExitEditMode":
                    {
                        tabItemRegions.IsEnabled = true;
                        tabItemBiomes.IsEnabled = true;
                        if (tabControlMap.Tag != null)
                        {
                            tabControlMap.SelectedItem = tabControlMap.Tag;
                            tabControlMap.Tag = null;
                        }
                        labelInfo.Text = "";
                        break;
                    }

                case "UpdateRegion":
                    {
                        RemoveZones(e.Region);
                        CheckZones(e.Region, false);
                        e.Region.Update();
                        LoadZones(e.Region);
                        saveCounter_++;
                        labelInfo.Text = $"Mise à jour de la région '{e.Region.Label}'";
                        buttonSaveRegions.IsEnabled = true;
                        break;
                    }

                case "SelectZone":
                    {
                        var zone = e.Object as MapZone;
                        listviewZones.SelectedItem = zone;
                        break;
                    }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (mapViewer.EditMode)
            {
                MessageBox.Show("Mode édition en cours, impossible de fermer l'application.");
                e.Cancel = true;
                return;
            }
            if (saveCounter_ > 0)
            {
                if (MessageBox.Show("Voulez-vous sauvegarder vos modifications ?", "Sauvegarde", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    UpdateRegions();
                    SaveRegions();
                }
            }
            // Save config
            SaveMainConfig();
        }

        public void ConsoleWriteLine(string text)
        {
            Console.WriteLine(text);
            textboxConsole.AppendText(text + Environment.NewLine);
        }

        private string getSplitterPos()
        {
            var converter = new GridLengthConverter();
            return $"{converter.ConvertToString(LeftColumn.Width)};{converter.ConvertToString(RightColumn.Width)}";
        }

        private void setSplitterPos(string splitterPos)
        {
            var split = splitterPos.Split(';');
            var converter = new GridLengthConverter();
            LeftColumn.Width = (GridLength)converter.ConvertFromString(split[0]);
            RightColumn.Width = (GridLength)converter.ConvertFromString(split[1]);
        }

        // TODO: Ajouter le chemin vers le fichier de traduction
        private void LoadMainConfig()
        {
            try
            {
                // Read config
                string cfg_filename = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".json");
                string cfg_lines = File.ReadAllText(cfg_filename);
                cfg_ = JsonSerializer.Deserialize<MainConfig>(cfg_lines);
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = cfg_.window.left;
                Top = cfg_.window.top;
                Width = cfg_.window.width;
                Height = cfg_.window.height;
                if (cfg_.splitter_pos != null)
                {
                    setSplitterPos(cfg_.splitter_pos);
                }
            }
            catch (FileNotFoundException)
            { }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading config ! ({0})", ex.Message);
            }
        }

        private void SaveMainConfig()
        {
            string cfg_filename = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".json");
            // Update config
            cfg_.window.left = Convert.ToInt32(Left);
            cfg_.window.top = Convert.ToInt32(Top);
            cfg_.window.width = Convert.ToInt32(Width);
            cfg_.window.height = Convert.ToInt32(Height);
            cfg_.splitter_pos = getSplitterPos();
            // Write config
            if (cfg_.obelisk_path == null)
            {
                cfg_.obelisk_path = @"<drive>:<obelisk_folder>\data\wiki\";
            }
            if (cfg_.translate_path == null)
            {
                cfg_.translate_path = @"<chemin vers le fichier ShooterGame.archive de ARK>";
            }
            string json = JsonSerializer.Serialize(cfg_, new JsonSerializerOptions 
            { 
                WriteIndented = true, 
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(cfg_filename, json);
        }

        private void comboBoxMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            lockInterface_++;
            textboxConsole.Clear();
            textEditorRegions.Text = null;
            ClearData();
            buttonLoadRegions.IsEnabled = false;
            buttonLoadBiomes.IsEnabled = false;
            buttonUpdateFromRegions.IsEnabled = false;
            buttonUpdateFromBiomes.IsEnabled = false;
            if (e.RemovedItems.Count > 0)
            {
                var map_item_prev = e.RemovedItems[0] as MapListJsonItem;
                if (map_item_prev != null)
                {
                }
            }
            var map_item = e.AddedItems[0] as MapListJsonItem;
            if (map_item != null)
            {
                mapViewer.Clear();

                mapViewer.MapName = map_item.name;
                if (map_item.map_border != null)
                {
                    mapViewer.MapBorderWidth = map_item.map_border.width;
                    mapViewer.MapBorderColor = (SolidColorBrush)new BrushConverter().ConvertFrom(map_item.map_border.color);
                }
                if (map_item.coordinateBorders != null)
                {
                    mapViewer.MapSize = new MapSize(map_item.coordinateBorders.left, map_item.coordinateBorders.top,
                                                    map_item.coordinateBorders.right, map_item.coordinateBorders.bottom);
                }
                if (map_item.map != null)
                {
                    mapViewer.MapImage = $"ARKRegionsEditor.Ressources.{map_item.map}";
                    if (map_item.map_border.width == null)
                    {
                        mapViewer.AutoAdjustBorder();
                    }
                }
                if (map_item.regions != null || map_item.regionsFile != null)
                {
                    buttonLoadRegions.IsEnabled = true;
                }
                tabControlMain.SelectedItem = tabItemMap;
                if (map_item.biomes != null)
                {
                    try
                    {
                        if (File.Exists(Path.Combine(cfg_.obelisk_path, map_item.biomes)))
                        {
                            buttonLoadBiomes.IsEnabled = true;
                        }
                    }
                    catch 
                    {
                        MessageBox.Show($"Erreur au chargement de '{cfg_.obelisk_path}/{map_item.biomes}'");
                    }
                }

                mapViewer.ZoomInFull();
            }
            lockInterface_--;
        }

        private void buttonLoadRegions_Click(object sender, RoutedEventArgs e)
        {
            var map_def = comboBoxMap.SelectedItem as MapListJsonItem;
            if (map_def.regionsFile != null)
            {
                textEditorRegions.Text = File.ReadAllText(map_def.regionsFile);
            }
            else if (map_def.regions != null)
            {
                textEditorRegions.Text = LoadStringFromRes($"ARKRegionsEditor.Ressources.{map_def.regions}");
            }
        }

        private void buttonLoadBiomes_Click(object sender, RoutedEventArgs e)
        {
            LoadBiomes(comboBoxMap.SelectedItem.ToString());
        }

        public string LoadStringFromRes(string res)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(res);
            if (stream == null)
            {
                Console.WriteLine($"Ressource {res} non trouvé !");
                return null;
            }
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // C:\Program Files (x86)\Steam\steamapps\common\ARK\ShooterGame\Content\Localization\Game\fr\ShooterGame.archive
        // => Subnamespaces / Children => [{Source / Text => Translation / Text}]
        public void LoadTranslations(string file_path)
        {
            translation_.Clear();
            reserveTranslation_.Clear();
            if (File.Exists(file_path))
            {
                var json_data = File.ReadAllText(file_path);
                var mainJson = JsonSerializer.Deserialize<GameTranslation>(json_data);

                // Recherche Subnamespaces 'Content'
                var sub = mainJson.Subnamespaces.Find(x => x.Namespace == "Content");
                if (sub != null)
                {
                    foreach (var item in sub.Children)
                    {
                        translation_[item.Source.Text] = item.Translation.Text;
                        reserveTranslation_[item.Translation.Text] = item.Source.Text;
                    }
                    ConsoleWriteLine($"{translation_.Count} traductions chargées");
                    checkBoxTranslate.IsEnabled = true;
                }
            }
            else
            {
                ConsoleWriteLine($"fichier de traduction {file_path} n'existe pas.");
            }
        }

        public void ClearData()
        {
            regionsList_.Clear();
            biomesList_.Clear();
            zonesList_.Clear();
            mapViewer.ClearZones();
        }

        public void LoadRegions(string jsonRegions)
        {
            buttonUpdateFromRegions.IsEnabled = false;
            ClearData();

            if (String.IsNullOrEmpty(jsonRegions))
            {
                return;
            }

            try
            {
                var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);

                ConsoleWriteLine($"Chargement des régions pour '{comboBoxMap.SelectedValue}'");
                jsonRegions_ = JsonSerializer.Deserialize<ArkWikiJsonRegions>(jsonRegions);
                if (jsonRegions_.coordinateBorders != null)
                {
                    if (map != null && map.coordinateBorders != null)
                    {
                        if (!jsonRegions_.coordinateBorders.Equals(map.coordinateBorders))
                        {
                            ConsoleWriteLine("Les coordonnées des bords ne sont pas les mêmes !");
                        }
                    }
                }

                ConsoleWriteLine($"{jsonRegions_.regions.Count} régions à analyser");
                var regions_list = new List<Region>();
                foreach (var region in jsonRegions_.regions)
                {
                    var region_name = region.Key;
                    if (checkBoxTranslate.IsChecked == true)
                    {
                        if (translation_.ContainsKey(region_name))
                        {
                            region_name = translation_[region_name];
                        }
                        else
                        {
                            ConsoleWriteLine($"Traduction pour '{region_name}' non trouvé");
                        }
                    }
                    var region_item = new Region()
                    {
                        Name = region.Key,
                        Label = region_name
                    };
                    // [longitude, latitude, longeur_longitude, longeur_latitude]
                    foreach (var zone_def in region.Value)
                    {
                        var zone = new MapZone()
                        {
                            Lon = zone_def[0],
                            Lat = zone_def[1],
                            LonLength = zone_def[2],
                            LatLength = zone_def[3]
                        };
                        if (checkboxRoundZone.IsChecked == true)
                        {
                            zone.RoundCoordinates();
                        }
                        region_item.zones.Add(zone);
                    }
                    CheckZones(region_item, false);
                    CheckIntersects(region_item);
                    regions_list.Add(region_item);
                }
                // Remove empty biomes
                var empty_list = new List<Region>();
                foreach (var item in regions_list)
                {
                    if (item.zones.Count == 0)
                    {
                        empty_list.Add(item);
                    }
                }
                foreach (var item in empty_list)
                {
                    regions_list.Remove(item);
                }
                int zones_ctr = 0;
                foreach (var item in regions_list)
                {
                    regionsList_.Add(item);
                    zones_ctr += item.ZoneCount;
                }
                BuildRegionPriority();
                listviewRegionsSort();
                ConsoleWriteLine($"{regionsList_.Count} régions chargées");
                ConsoleWriteLine($"{zones_ctr} zones chargées");
                // Mise à jour en différé sinon ça plante
                var timer_ur = new Timer(TimerCallback_UpdateRegions, null, 10, 0);
                buttonUpdateFromRegions.IsEnabled = true;
                tabControlMap.SelectedItem = tabItemRegions;
                mapViewer.RescaleCanvas(mapViewer.Scale);
            }
            catch (Exception ex) {
                MessageBox.Show("Error loading region ! ({0})", ex.Message);
            }
        }

        private void SaveDataRegions()
        {
            // Check if a file exists
            var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);
            if (map.regionsFile == null)
            {
                if (map.regions != null)
                {
                    map.regionsFile = map.regions;
                }
                else
                {
                    map.regionsFile = map.name.Replace(' ', '_') + "_Regions.json";
                }
                if (cfg_.maps == null)
                {
                    cfg_.maps = new Dictionary<string, MapListJsonItem>();
                }
                MapListJsonItem map_def = null;
                if (cfg_.maps.ContainsKey(map.name))
                {
                    map_def = cfg_.maps[map.name];
                }
                else
                {
                    map_def = new MapListJsonItem()
                    {
                        regions = map.regionsFile
                    };
                    cfg_.maps[map.name] = map_def;
                }
                SaveMainConfig();
            }
            File.WriteAllText(map.regionsFile, textEditorRegions.Text);
            saveCounter_ = 0;
            buttonSaveRegions.IsEnabled = false;
        }

        private void SaveRegions()
        {
            UpdateRegions();
            SaveDataRegions();
        }

        void BuildRegionPriority()
        {
            int priority = 0;
            foreach (var region in regionsList_)
            {
                region.Priority = priority++;
            }
        }

        void TimerCallback_UpdateRegions(Object state)
        {
            textEditorRegions.Dispatcher.BeginInvoke(new Action(() => { UpdateRegions(); }));
        }

        public void UpdateRegions()
        {
            buttonSaveRegions.IsEnabled = false;
            buttonSaveDataRegions.IsEnabled = false;
            if (jsonRegions_ != null)
            {
                string str;
                var json_lines = new List<string>();
                // Attention le formattage du json doit être compacte mais pas sur une seule ligne !
                json_lines.Add("{");
                if (jsonRegions_.coordinateBorders != null)
                {
                    var cb = jsonRegions_.coordinateBorders;
                    str = $"    'coordinateBorders': {{ 'top': {cvt_float_string(cb.top)}, 'left': {cvt_float_string(cb.left)}, " +
                        $"'bottom': {cvt_float_string(cb.bottom)}, 'right': {cvt_float_string(cb.right)} }},";
                    json_lines.Add(str.Replace('\'', '"'));
                }
                json_lines.Add("    \"regions\": {");
                jsonRegions_.regions.Clear();
                foreach (var region in regionsList_)
                {
                    StringBuilder str_zones = new StringBuilder(2048);
                    str_zones.Append($"        \"{region.Label}\":[");
                    var zones_list = new List<List<float>>();
                    foreach (var zone_src in region.zones)
                    {
                        var zone_dst = new List<float>();
                        zone_dst.Add(zone_src.Lon);
                        zone_dst.Add(zone_src.Lat);
                        zone_dst.Add(zone_src.LonLength);
                        zone_dst.Add(zone_src.LatLength);
                        zones_list.Add(zone_dst);
                        str_zones.Append("[");
                        foreach (var z in zone_dst)
                        {
                            str = cvt_float_string(z);
                            str_zones.Append($"{str},");
                        }
                        str_zones.Remove(str_zones.Length - 1, 1);
                        str_zones.Append("],");
                    }
                    str_zones.Remove(str_zones.Length - 1, 1);
                    str_zones.Append("],");
                    jsonRegions_.regions.Add(region.Label, zones_list);
                    json_lines.Add(str_zones.ToString());
                }
                str = json_lines[json_lines.Count - 1];
                json_lines[json_lines.Count - 1] = str.Remove(str.Length - 1, 1);
                json_lines.Add("    }" + (jsonRegions_.forceDLC != null ? "," : ""));
                if (jsonRegions_.forceDLC != null)
                {
                    str = $"    'forceDLC': [ ";
                    foreach (var dlc in jsonRegions_.forceDLC)
                    {
                        str += $"'{dlc}', ";
                    }
                    str = str.Remove(str.Length - 2, 2);
                    str += " ]";
                    json_lines.Add(str.Replace('\'', '"'));
                }
                json_lines.Add("}");
                lockInterface_++;
                textEditorRegions.Text = String.Join("\n", json_lines);
                lockInterface_--;
                //buttonSaveRegions.IsEnabled = true;
                buttonSaveDataRegions.IsEnabled = true;
            }
        }

        // Charge un fichier de biomes de Purlovia / Obelisk
        public void LoadBiomes(string map_name)
        {
            var map = mapList_.maps.Find(x => x.name == map_name);
            var path = Path.Combine(cfg_.obelisk_path, map.biomes);
            var json_data = File.ReadAllText(path);
            var mainJson = JsonSerializer.Deserialize<ObeliskJsonBiomes>(json_data);

            buttonUpdateFromBiomes.IsEnabled = false;
            biomesList_.Clear();

            ConsoleWriteLine($"{mainJson.biomes.Count} biomes à analyser");
            var biomes_list = new List<Region>();
            foreach (var biome in mainJson.biomes)
            {
                if (!String.IsNullOrEmpty(biome.name))
                {
                    var region = biomes_list.FirstOrDefault(x => x.Label == biome.name);
                    if (region == null)
                    {
                        region = new Region(biome);
                        biomes_list.Add(region);
                    }
                    foreach (var zone in biome.boxes)
                    {
                        var dz = new MapZone(zone, map.coordinateBorders);
                        if (dz.LonLength >= 0 && dz.LatLength >= 0)
                        {
                            if (!CheckDuplicate(region.zones, dz))
                            {
                                region.zones.Add(dz);

                                if (dz.LonLength < 2 && dz.LatLength < 2)
                                {
                                    region.Warning = true;
                                }
                            }
                            else
                            {
                                ConsoleWriteLine($"Doublon trouvé pour {biome.name} : ({zone.start.lat};{zone.start.lon})");
                            }
                        }
                        else
                        {
                            ConsoleWriteLine($"Biome '{biome.name}': zone ({dz.Lat};{dz.Lon};{dz.LatLength};{dz.LonLength}) supprimée car trop petite");
                        }
                    }
                }
                else
                {
                    var dz = biome.boxes[0];
                    ConsoleWriteLine($"Biome '{biome.name}': zone ({dz.start.lat};{dz.start.lon}) supprimée car pas de nom");
                }
            }
            labelInfo.Text = $"{biomes_list.Count} biomes chargés";
            ConsoleWriteLine(labelInfo.Text);

            // Remove empty biomes
            var empty_list = new List<Region>();
            foreach (var item in biomes_list)
            {
                if (item.zones.Count == 0)
                {
                    empty_list.Add(item);
                }
            }
            foreach (var item in empty_list)
            {
                biomes_list.Remove(item);
            }

            // Chargement par ordre de priortité et non alphabétique
            foreach (var bbs in biomes_list.OrderBy(item => item.Priority))
            {
                CheckZones(bbs, false);
                CheckIntersects(bbs);
                biomesList_.Add(bbs);
            }
            tabControlMap.SelectedItem = tabItemBiomes;
            buttonUpdateFromBiomes.IsEnabled = true;
        }

        public void BuildRegionsFromBiomes()
        {
            if (checkBoxTranslate.IsChecked == true)
            {
                listviewRegionsColumnName.Width = 100;
            }
            regionsList_.Clear();
            foreach (var biome in biomesList_)
            {
                if (checkBoxTranslate.IsChecked == true)
                {
                    if (translation_.ContainsKey(biome.Name))
                    {
                        biome.Label = translation_[biome.Name];
                    }
                    else
                    {
                        ConsoleWriteLine($"Traduction pour '{biome.Name}' non trouvé");
                    }
                }
                regionsList_.Add(biome);
            }
            var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);
            jsonRegions_ = new ArkWikiJsonRegions();
            if (map.coordinateBorders != null)
            {
                jsonRegions_.coordinateBorders = map.coordinateBorders;
            }
            jsonRegions_.regions = new Dictionary<string, List<List<float>>>();
        }

        // Recherche 'zone' dans 'biomes'
        public bool CheckDuplicate(List<MapZone> zones, MapZone zone)
        {
            foreach (var dst in zones)
            {
                if (dst.Equals(zone))
                {
                    return true;
                }
            }
            return false;
        }

        public MapZone CheckInside(List<MapZone> zones, MapZone zone)
        {
            foreach (var dst in zones)
            {
                if (dst != zone)
                {
                    if (dst.Contains(zone))
                    {
                        return dst;
                    }
                }
            }
            return null;
        }

        public void CheckIntersects(Region region)
        {
            foreach (var zone in region.zones)
            {
                CheckIntersectsWith(region, zone);
            }
        }

        public MapZone CheckIntersectsWith(Region region, MapZone zone)
        {
            foreach (var dst in region.zones)
            {
                if (dst != zone)
                {
                    if (dst.IntersectsWith(zone))
                    {
                        var intersect = MapZone.Intersect(dst, zone);
                        if (intersect.LatLength != 0 && intersect.LonLength != 0)
                        {
                            region.Warning = true;
                            return dst;
                        }
                    }
                }
            }
            return null;
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void CheckZones(Region region, bool selection)
        {
            // Check Zones
            int zone_inside = 0;
            int zone_intersect = 0;
            if (selection)
                mapViewer.ClearHighlightZones();
            region.Error = false;
            region.Warning = false;
            foreach (var zone in region.zones)
            {
                int highlight = 0;
                var zdst = CheckInside(region.zones, zone);
                if (zdst != null)
                {
                    zone.Inside = true;
                    zone_inside++;
                    highlight++;
                    if (selection)
                        mapViewer.HighlightZone(zone, Colors.White);
                    region.Error = true;
                }
                zdst = CheckIntersectsWith(region, zone);
                if (zdst != null)
                {
                    if (highlight == 0)
                    {
                        zone_intersect++;
                        if (selection)
                            mapViewer.HighlightZone(zone, Colors.Cyan);
                    }
                }
            }
            if (selection == false && checkboxRemoveInsideZone.IsChecked == true)
            {
                RemoveInsideZones(region);
            }
            if (selection)
                labelInfo.Text = $"Zones chargées: {zonesList_.Count}, zones incluses : {zone_inside}, zones avec intersection : {zone_intersect}";
        }

        private void LoadZones(Region region)
        {
            mapViewer.ClearZones();
            mapViewer.LoadZones(region);
            zonesList_.Clear();
            foreach (var zone in region.zones)
            {
                zonesList_.Add(zone);
            }
            listviewZones.Tag = region;
        }

        private void listviewRegions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var region = e.AddedItems[0] as Region;
                LoadZones(region);
                CheckZones(region, true);
            }
        }

        private void listviewRegions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var region = listviewRegions.SelectedItem as Region;
            if (region != null)
            {
                mapViewer.ZoomToMapPos(region.zones[0].Center, true, Colors.White);
            }
        }

        private void listviewRegions_KeyDown(object sender, KeyEventArgs e)
        {
            if (lockKeyboard_ != 0) return;
            // Ctrl-C
            if (e.Key == Key.C && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
            {
                var region = listviewRegions.SelectedItem as Region;
                if (region != null)
                {
                    Clipboard.SetText(region.Label);
                    ConsoleWriteLine(region.Label);
                }
                lockKeyboard_++;
            }
            // Del
            else if (e.Key == Key.Delete)
            {
                var region = listviewRegions.SelectedItem as Region;
                if (region != null)
                {
                    regionsList_.Remove(region);
                    ConsoleWriteLine($"Suppression de la région '{region.Label}'");
                }
                lockKeyboard_++;
            }
        }

        private void listviewRegions_KeyUp(object sender, KeyEventArgs e)
        {
            lockKeyboard_ = 0;
        }

        private bool RemoveInsideZones(Region region)
        {
            // Check Zones
            var list_to_remove = new List<MapZone>();
            foreach (var zone in region.zones)
            {
                var zdst = CheckInside(region.zones, zone);
                if (zdst != null)
                {
                    list_to_remove.Add(zone);
                }
            }
            foreach (var item in list_to_remove)
            {
                region.zones.Remove(item);
            }
            region.Error = false;
            region.Update();
            return list_to_remove.Count > 0;
        }

        private void RemoveZones(Region region)
        {
            var list_to_remove = region.zones.FindAll(x => x.MarkToDelete);
            foreach (var item in list_to_remove)
            {
                region.zones.Remove(item);
            }
            region.Update();
        }

        private void listviewBiomes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var biome = e.AddedItems[0] as Region;
                LoadZones(biome);
                CheckZones(biome, true);
            }
        }

        private void listviewBiomes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var biome = listviewBiomes.SelectedItem as Region;
            if (biome != null)
            {
                mapViewer.ZoomToMapPos(biome.zones[0].Center, true, Colors.White);
                mapViewer.ClearHighlightZones();
            }
        }

        private void listviewBiomes_KeyUp(object sender, KeyEventArgs e)
        {
            // Ctrl-C
            if (e.Key == Key.C && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
            {
                var region = listviewBiomes.SelectedItem as Region;
                if (region != null)
                {
                    Clipboard.SetText(region.Label);
                    ConsoleWriteLine(region.Label);
                }
            }
            // Del
            else if (e.Key == Key.Delete)
            {
                var biome = listviewBiomes.SelectedItem as Region;
                if (biome != null)
                {
                    biomesList_.Remove(biome);
                    ConsoleWriteLine($"Suppression du biome '{biome.Label}'");
                }
            }
        }

        private void listviewZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var zone = e.AddedItems[0] as MapZone;
                mapViewer.ClearHighlightZones();
                mapViewer.HighlightZone(zone, Colors.White);
            }
        }

        private void listviewZones_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var zone = listviewZones.SelectedItem as MapZone;
            if (zone != null)
            {
                mapViewer.ZoomToMapPos(zone.Center, true, Colors.White);
            }
        }

        private void listviewZones_KeyUp(object sender, KeyEventArgs e)
        {
        }

        private void textEditorRegions_TextChanged(object sender, EventArgs e)
        {
            if (lockInterface_ != 0) return;
            LoadRegions(textEditorRegions.Text);
        }

        private void comboBoxZonesColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            mapViewer.ZonesColor = (e.AddedItems[0] as ColorItem).Color;
        }

        private void sliderZonesOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lockInterface_ != 0 || mapViewer == null) return;
            mapViewer.ZonesOpacity = (int)e.NewValue;
        }

        private string cvt_float_string(float value)
        {
            float partieDecimale = value - (float)Math.Floor(value);
            if (partieDecimale == 0)
            {
                return $"{value}";
            }
            else
            {
                return $"{value.ToString("F2", CultureInfo.InvariantCulture)}";
            }
        }

        private void buttonUpdateFromRegions_Click(object sender, RoutedEventArgs e)
        {
            UpdateRegions();
        }

        private void buttonUpdateFromBiomes_Click(object sender, RoutedEventArgs e)
        {
            BuildRegionsFromBiomes();
            UpdateRegions();
        }

        private void buttonSaveDataRegions_Click(object sender, RoutedEventArgs e)
        {
            SaveDataRegions();
        }

        private void buttonSaveRegions_Click(object sender, RoutedEventArgs e)
        {
            SaveRegions();
        }

        #region Listview Region Drag and Drop
        Point startPoint;
        private void listviewRegions_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
        }

        // Helper to search up the VisualTree
        private static T FindAnchestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private void listviewRegions_MouseMove(object sender, MouseEventArgs e)
        {
            if (startPoint.X != 0)
            {
                // Get the current mouse position
                Point mousePos = e.GetPosition(null);
                Vector diff = startPoint - mousePos;

                if (e.LeftButton == MouseButtonState.Pressed &&
                    Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Get the dragged ListViewItem
                    ListView listView = sender as ListView;
                    ListViewItem listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                    if (listViewItem != null)
                    {
                        // Find the data behind the ListViewItem
                        var region = (Region)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                        // Initialize the drag & drop operation
                        DataObject dragData = new DataObject();
                        dragData.SetData("RegionSource", region);
                        DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
                    }
                }
            }
        }

        private void listviewRegions_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            startPoint.X = 0;
        }

        private void listviewRegions_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("RegionSource") || sender != e.Source)
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                ListView listView = sender as ListView;
                ListViewItem listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem != null)
                {
                    // Find the data behind the ListViewItem
                    var region = (Region)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                    e.Data.SetData("RegionDestination", region);
                }
            }
        }

        private void listviewRegions_Drop(object sender, DragEventArgs e)
        {
            startPoint.X = 0;
            if (e.Data.GetDataPresent("RegionSource") && e.Data.GetDataPresent("RegionDestination"))
            {
                Region region_src = e.Data.GetData("RegionSource") as Region;
                Region region_dst = e.Data.GetData("RegionDestination") as Region;
                int idx_dst = regionsList_.IndexOf(region_dst);
                if (idx_dst >= 0)
                {
                    regionsList_.Remove(region_src);
                    regionsList_.Insert(idx_dst, region_src);
                    BuildRegionPriority();
                    listviewRegionsSort();
                }
            }
        }

        private void listviewRegions_DragLeave(object sender, DragEventArgs e)
        {
            startPoint.X = 0;
        }
        #endregion

        private void checkboxViewRegion_Checked(object sender, RoutedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            if (mapViewer != null)
                mapViewer.RegionVisibility = checkboxViewRegion.IsChecked;
        }

        private void checkboxViewRegionWikiStyle_Checked(object sender, RoutedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            if (mapViewer != null)
                mapViewer.RegionWikiStyle = checkboxRegionWikiStyle.IsChecked;
        }

        private void checkboxViewZone_Checked(object sender, RoutedEventArgs e)
        {
            if (lockInterface_ != 0) return;
            if (mapViewer != null)
                mapViewer.ZonesVisibility = checkboxViewZone.IsChecked;
        }

        private void listviewRegionsSort()
        {
            if (listViewRegionsSortCol_ != null)
            {
                listviewRegions.Items.SortDescriptions.Clear();
                listviewRegions.Items.SortDescriptions.Add(listViewRegionsSortDescription_);
                listviewRegions.Items.Refresh();
            }
        }

        private void listviewRegionsColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (sender as GridViewColumnHeader);
            string sortBy = column.Tag.ToString();
            if (listViewRegionsSortCol_ != null)
            {
                AdornerLayer.GetAdornerLayer(listViewRegionsSortCol_).Remove(listViewRegionsSortAdorner_);
                listviewRegions.Items.SortDescriptions.Clear();
            }
            ListSortDirection newDir = ListSortDirection.Ascending;
            if (listViewRegionsSortCol_ == column && listViewRegionsSortAdorner_.Direction == newDir)
                newDir = ListSortDirection.Descending;

            listViewRegionsSortCol_ = column;
            listViewRegionsSortAdorner_ = new SortAdorner(listViewRegionsSortCol_, newDir);
            AdornerLayer.GetAdornerLayer(listViewRegionsSortCol_).Add(listViewRegionsSortAdorner_);
            listViewRegionsSortDescription_ = new SortDescription(sortBy, newDir);
            listviewRegions.Items.SortDescriptions.Add(listViewRegionsSortDescription_);
        }
    }
}