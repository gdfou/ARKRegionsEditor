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
using System.Globalization;
using System.Windows.Input;
using System.Windows.Documents;
using System.Text;
using SyntaxEdit.Editing;
using System.Xml.Linq;
using System.Windows.Threading;

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
        Dictionary<string, string> reverseTranslation_;
        ObservableCollection<Region> regionsList_;
        ObservableCollection<MapZone> zonesList_;
        ObservableCollection<ColorItem> colorList_;
        GridViewColumnHeader listViewRegionsSortCol_;
        SortAdorner listViewRegionsSortAdorner_;
        SortDescription listViewRegionsSortDescription_;
        int saveCounter_;
        int lockKeyboard_;

        public MainWindow()
        {
            translation_ = new Dictionary<string, string>();
            reverseTranslation_ = new Dictionary<string, string>();
            regionsList_ = new ObservableCollection<Region>();
            zonesList_ = new ObservableCollection<MapZone>();
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
                    window = new JsonRect()
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
            comboBoxMap.Items.Add("Nouvelle carte");
            comboBoxMap.Items.Add(new Separator());
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
                var new_maps = new List<MapListJsonItem>();
                foreach (var map in cfg_.maps)
                {
                    var map_def = mapList_.maps.Find(x => x.name == map.Key);
                    if (map_def != null)
                    {
                        map_def.regionsFile = map.Value.regions;
                    }
                    else
                    {
                        map.Value.name = map.Key;
                        new_maps.Add(map.Value);
                        mapList_.maps.Add(map.Value);
                    }
                }
                if (new_maps.Count > 0)
                {
                    comboBoxMap.Items.Add(new Separator());
                    foreach (var map in new_maps)
                    {
                        comboBoxMap.Items.Add(map);
                    }
                }
            }

            mapViewer.ZoomInFull();
            mapViewer.MaxScale = 8;

            mapViewer.CommandEvent += MapViewer_CommandEvent;

            checkboxViewRegion.IsChecked = mapViewer.RegionVisibility;
            checkboxViewZone.IsChecked = mapViewer.ZonesVisibility;

            listviewRegions.ItemsSource = regionsList_;
            listviewZones.ItemsSource = zonesList_;

            comboBoxZonesColor.SelectedItem = colorList_.First(x => x.Color == mapViewer.ZonesColor);
            sliderZonesOpacity.Value = mapViewer.ZonesOpacity;
            if (File.Exists(cfg_.translate_path) == false)
            {
                cfg_.translate_path = null;
                if (MessageBox.Show("Veulliez sélectionner le fichier de traduction d'ARK: Survival Evolved\n" +
                    "Le fichier ShooterGame.archive ce trouve dans le dossier 'ShooterGame\\Content\\Localization\\Game\\<langue>' du jeu", 
                    "Fichier de traduction", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var openFileDialog = new Microsoft.Win32.OpenFileDialog()
                    {
                        Filter = "Fichier de traduction|ShooterGame.archive",
                        Title = "Sélectionnez le fichier de traduction 'ShooterGame.archive'"
                    };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        cfg_.translate_path = openFileDialog.FileName;
                    }
                }
            }
            LoadTranslations(cfg_.translate_path);

            listviewRegionsColumnName.Width = 0;
            tabControlMain.SelectedItem = tabItemMap;
            labelInfo.Text = "Choisissez une carte, puis chargez les régions ou les biomes.";

            lockInterface_--;
        }

        private void IncSaveCounter()
        {
            saveCounter_++;
            buttonSaveRegions.IsEnabled = true;
            BuildJsonRegionsData();
        }

        private void MapViewer_CommandEvent(object sender, CommandEventArgs e)
        {
            switch(e.Command)
            {
                case "EnterEditMode":
                    {
                        tabItemDataRegions.IsEnabled = false;
                        tabItemRegions.IsEnabled = false;
                        tabControlMap.Tag = tabControlMap.SelectedItem;
                        tabControlMap.SelectedItem = tabItemZones;
                        labelInfo.Text = "mode édition";
                        break;
                    }

                case "ExitEditMode":
                    {
                        tabItemDataRegions.IsEnabled = true;
                        tabItemRegions.IsEnabled = true;
                        listviewZones.IsEnabled = true;
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
                        // Check for new zone
                        foreach (var zone in zonesList_)
                        {
                            if (zone.NewZone)
                            {
                                e.Region.zones.Add(zone);
                            }
                        }
                        RemoveZones(e.Region);
                        CheckZones(e.Region);
                        e.Region.Update();
                        LoadZones(e.Region);
                        IncSaveCounter();
                        labelInfo.Text = $"Mise à jour de la région '{e.Region.Label}'";
                        break;
                    }

                case "SelectZone":
                    {
                        var zone = e.Object as MapZone;
                        listviewZones.SelectedItem = zone;
                        break;
                    }

                case "CopyZone":
                    {
                        var zone = e.Object as MapZone;
                        var new_zone = new MapZone();
                        new_zone.CopyFrom(zone);
                        new_zone.NewZone = true;
                        zonesList_.Add(new_zone);
                        mapViewer.AddZone(new_zone);
                        listviewZones.SelectedItem = zone;
                        break;
                    }

                case "StartEditZone":
                    {
                        //var zone = e.Object as MapZone;
                        listviewZones.IsEnabled = false;
                        break;
                    }

                case "StopEditZone":
                    {
                        //var zone = e.Object as MapZone;
                        listviewZones.IsEnabled = true;
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
            if (saveCounter_ > 0)
            {
                if (MessageBox.Show("Voulez-vous sauvegarder vos modifications ?", "Sauvegarde", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    SaveRegions();
                }
            }

            if (lockInterface_ != 0) return;
            lockInterface_++;
            textboxConsole.Clear();
            textEditorRegions.Text = null;
            ClearData();
            buttonLoadRegions.IsEnabled = true;
            buttonLoadBiomes.IsEnabled = true;
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
                // Gestion at et conversion vers coordinateBorders
                if ((map_item.at != null) && (map_item.at.Count == 2) && (map_item.at[0].Count == 2) && (map_item.at[1].Count == 2))
                {
                    mapViewer.MapSize = new MapSize(map_item.at[0][1], map_item.at[0][0], map_item.at[1][1], map_item.at[1][0]);
                }
                else if (map_item.coordinateBorders != null)
                {
                    mapViewer.MapSize = new MapSize(map_item.coordinateBorders.left, map_item.coordinateBorders.top,
                                                    map_item.coordinateBorders.right, map_item.coordinateBorders.bottom);
                }
                if (map_item.map != null)
                {
                    mapViewer.MapImage = $"ARKRegionsEditor.Ressources.{map_item.map}";
                }
                else if (map_item.mapFile != null)
                {
                    mapViewer.MapImage = map_item.mapFile;
                }
                if (map_item.map_border?.width == null)
                {
                    mapViewer.AutoAdjustBorder();
                }
                tabControlMain.SelectedItem = tabItemMap;
                mapViewer.ZoomInFull();
            }
            else // nouvelle carte
            {
                mapViewer.Clear();
                var openFileDialog = new Microsoft.Win32.OpenFileDialog()
                {
                    Filter = "ARK Map Image File|*.jpg",
                    Title = "Sélectionnez le fichier image de la carte"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    mapViewer.MapImage = openFileDialog.FileName;
                    CreateMapSaveData(openFileDialog.FileName);
                    SaveMainConfig();
                    MessageBox.Show("L'application va se fermer\nModifier le fichier de configuration avec les données de cette carte.");
                    Close();
                }
            }
            lockInterface_--;
        }

        private void LoadRegionsQuery(string jsonString)
        {
            if (MessageBox.Show("Cette opération va remplacer toutes les données de régions, voulez-vous continuer ?", "Charger des données de régions", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                LoadRegionsFromJson(jsonString);
            }
        }

        private string BuildAndLoadRegionFile(string regionsFile)
        {
            if (regionsFile != null)
            {
                var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var fullFilename = Path.Combine(exePath, regionsFile);
                if (File.Exists(fullFilename))
                {
                    return File.ReadAllText(fullFilename);
                }
            }
            return null;
        }

        private void buttonLoadRegions_Click(object sender, RoutedEventArgs e)
        {
            // Chargement à partir de la ressource interne, d'un fichier json de sauvegarde ou d'un fichier externe
            var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);
            var str = BuildAndLoadRegionFile(map.regionsFile);
            if (str != null)
            {
                LoadRegionsQuery(str);
            }
            else if (map.regions != null)
            {
                LoadRegionsQuery(LoadStringFromRes($"ARKRegionsEditor.Ressources.{map.regions}"));
            }
            else
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog()
                {
                    Filter = "ARK Wiki Regions Data File|*.json",
                    Title = "Sélectionnez le fichier de données de régions à importer"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    LoadRegionsQuery(File.ReadAllText(openFileDialog.FileName));
                }
            }
        }

        private void buttonLoadBiomes_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Cette opération va remplacer toutes les données de régions, voulez-vous continuer ?", "Charger un fichier de biomes", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // Chargement à partir du lien d'un fichier externe
                var openFileDialog = new Microsoft.Win32.OpenFileDialog()
                {
                    Filter = "Obelisk Biomes Data File|*.json",
                    Title = "Sélectionnez le fichier de données de biomes à importer"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    LoadBiomes(openFileDialog.FileName);
                }
            }
        }

        private void textEditorRegions_TextChanged(object sender, EventArgs e)
        {
            if (lockInterface_ != 0) return;
            LoadRegionsQuery(textEditorRegions.Text);
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
            reverseTranslation_.Clear();
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
                        reverseTranslation_[item.Translation.Text] = item.Source.Text;
                    }
                    ConsoleWriteLine($"{translation_.Count} traductions chargées");
                    checkBoxTranslate.IsEnabled = true;
                }
            }
            else
            {
                ConsoleWriteLine($"fichier de traduction {file_path} n'existe pas.");
                tabControlMain.SelectedItem = tabItemConsole;
            }
        }

        public void ClearData()
        {
            saveCounter_ = 0;
            regionsList_.Clear();
            zonesList_.Clear();
            mapViewer.ClearZones();
        }

        public void LoadRegionsFromJson(string jsonString)
        {
            if (String.IsNullOrEmpty(jsonString))
            {
                ConsoleWriteLine("le fichier de données est vide.");
                tabControlMain.SelectedItem = tabItemConsole;
                return;
            }

            var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);
            var jsonRegions = JsonSerializer.Deserialize<ArkWikiJsonRegions>(jsonString);
            if (jsonRegions.coordinateBorders != null)
            {
                if (map != null && map.coordinateBorders != null)
                {
                    if (!jsonRegions.coordinateBorders.Equals(map.coordinateBorders))
                    {
                        ConsoleWriteLine("Les coordonnées des bords ne sont pas les mêmes !");
                        tabControlMain.SelectedItem = tabItemConsole;
                    }
                }
            }
            ConsoleWriteLine($"{jsonRegions.regions.Count} régions à analyser");

            // Construction de la liste des régions à partir des données de régions du wiki
            var regions_list = new List<Region>();
            foreach (var region in jsonRegions.regions)
            {
                var region_item = new Region()
                {
                    Name = region.Key,
                    Label = region.Key
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
                    region_item.zones.Add(zone);
                }
                regions_list.Add(region_item);
            }
            LoadRegions(regions_list);
        }

        // Utiliser à partir de LoadRegionsFromJson et de LoadBiomes
        public void LoadRegions(List<Region> regionslist)
        {
            lockInterface_++;
            ClearData();
            try
            {
                ConsoleWriteLine($"Chargement des régions pour '{mapViewer.MapName}'");
                var regions_list = new List<Region>();
                foreach (var region in regionslist)
                {
                    if (checkBoxTranslate.IsChecked == true)
                    {
                        if (translation_.ContainsKey(region.Name))
                        {
                            region.Label = translation_[region.Name];
                        }
                        else if (reverseTranslation_.ContainsKey(region.Name))
                        {
                            region.Label = reverseTranslation_[region.Name];
                        }
                        else
                        {
                            ConsoleWriteLine($"Traduction pour '{region.Name}' non trouvé");
                        }
                    }
                    // [longitude, latitude, longeur_longitude, longeur_latitude]
                    foreach (var zone in region.zones)
                    {
                        zone.RoundCoordinates();
                    }
                    // Trie des zones ?
                    //region.zones.Sort(MapZone.CompareTo);
                    CheckZones(region);
                    regions_list.Add(region);
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
                mapViewer.RescaleCanvas(mapViewer.Scale);
                labelInfo.Text = $"{regionsList_.Count} régions chargées ({zones_ctr} zones)";
                BuildJsonRegionsData();
                tabControlMap.SelectedItem = tabItemRegions;
                tabControlMain.SelectedItem = tabItemMap;
                if (checkBoxTranslate.IsChecked == true)
                {
                    listviewRegionsColumnName.Width = 120;
                }
            }
            catch (Exception ex) {
                MessageBox.Show("Error loading region ! ({0})", ex.Message);
            }
            lockInterface_--;
        }

        private string MapNameFromFile(string fullFilename)
        {
            // Genesis_Part_1_Topographic_Map.jpg
            // Genesis_Part_2_Map.jpg
            var filename = Path.GetFileName(fullFilename);
            int pos = filename.IndexOf("Topographic");
            if (pos >= 0)
            {
                return filename.Substring(0, pos - 1).Replace('_',' ');
            }
            else if((pos = filename.IndexOf("Map")) >= 0)
            {
                return filename.Substring(0, pos - 1).Replace('_', ' ');
            }
            return filename;
        }

        private void CreateMapSaveData(string mapFilename)
        {
            if (cfg_.maps == null)
            {
                cfg_.maps = new Dictionary<string, MapListJsonItem>();
            }
            var map = new MapListJsonItem()
            {
                mapFile = mapFilename,
                at = new List<List<float>> { new List<float> { 0, 0 }, new List<float> { 100, 100 } }
            };
            var map_name = MapNameFromFile(mapFilename);
            cfg_.maps[map_name] = map;
        }

        private void SaveDataRegions(string str)
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
            File.WriteAllText(map.regionsFile, str);
            saveCounter_ = 0;
            buttonSaveRegions.IsEnabled = false;
        }

        private void SaveRegions()
        {
            SaveDataRegions(BuildJsonRegionsData());
        }

        void BuildRegionPriority()
        {
            int priority = 0;
            foreach (var region in regionsList_)
            {
                region.Priority = priority++;
            }
        }

        // Construction du json de données et mise à jour de textEditorRegions
        public string BuildJsonRegionsData()
        {
            string str;
            var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);
            // Attention le formattage du json doit être compacte mais pas sur une seule ligne !
            var json_lines = new List<string>();
            json_lines.Add("{");
            if (map.coordinateBorders != null)
            {
                var cb = map.coordinateBorders;
                str = $"    'coordinateBorders': {{ 'top': {cvt_float_string(cb.top)}, 'left': {cvt_float_string(cb.left)}, " +
                    $"'bottom': {cvt_float_string(cb.bottom)}, 'right': {cvt_float_string(cb.right)} }},";
                json_lines.Add(str.Replace('\'', '"'));
            }
            json_lines.Add("    \"regions\": {");
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
                json_lines.Add(str_zones.ToString());
            }
            str = json_lines[json_lines.Count - 1];
            json_lines[json_lines.Count - 1] = str.Remove(str.Length - 1, 1);
            json_lines.Add("    }");
            json_lines.Add("}");
            str = String.Join("\n", json_lines);
            // Utilisation d'un DispatcherTimer pour éviter un crash si appel à partir de textEditorRegions
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += timer_Tick;
            timer.Tag = str;
            timer.Start();
            return str;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;
            timer.Stop();
            lockInterface_++;
            textEditorRegions.Text = timer.Tag as string;
            lockInterface_--;
        }

        // Charge un fichier de biomes de Purlovia / Obelisk
        public void LoadBiomes(string filename)
        {
            var json_data = File.ReadAllText(filename);
            var mainJson = JsonSerializer.Deserialize<ObeliskJsonBiomes>(json_data);
            // Construction de la liste des régions puis appel de LoadRegions
            ConsoleWriteLine($"{mainJson.biomes.Count} biomes à analyser");
            var map = mapList_.maps.Find(x => x.name == mapViewer.MapName);
            var regions_list = new List<Region>();
            foreach (var biome in mainJson.biomes)
            {
                if (!String.IsNullOrEmpty(biome.name))
                {
                    var region = regions_list.FirstOrDefault(x => x.Label == biome.name);
                    if (region == null)
                    {
                        region = new Region(biome);
                        regions_list.Add(region);
                    }
                    foreach (var zone in biome.boxes)
                    {
                        var dz = new MapZone(zone, map.coordinateBorders);
                        if (!CheckDuplicate(region.zones, dz))
                        {
                            region.zones.Add(dz);
                        }
                        else
                        {
                            ConsoleWriteLine($"Doublon trouvé pour {biome.name} : ({zone.start.lat};{zone.start.lon})");
                        }
                    }
                }
                else
                {
                    var dz = biome.boxes[0];
                    ConsoleWriteLine($"Biome '{biome.name}': zone ({dz.start.lat};{dz.start.lon}) supprimée car pas de nom");
                }
            }
            LoadRegions(regions_list);
            IncSaveCounter();
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
            region.Warning = false;
            foreach (var zone in region.zones)
            {
                CheckIntersectsWith(region, zone);
            }
        }

        public MapZone CheckIntersectsWith(Region region, MapZone zone)
        {
            foreach (var dst in region.zones)
            {
                if (dst != zone && !zone.Warning && !dst.Warning)
                {
                    if (dst.IntersectsWith(zone))
                    {
                        var intersect = MapZone.Intersect(dst, zone);
                        if (intersect != null && intersect.LatLength != 0 && intersect.LonLength != 0)
                        {
                            region.Warning = true;
                            dst.Warning = true;
                            zone.Warning = true;
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

        private void CheckZones(Region region)
        {
            // Check Zones
            RemoveInsideZones(region);
            CheckIntersects(region);
        }

        private void LoadZones(Region region, bool highlight=false)
        {
            mapViewer.ClearZones();
            mapViewer.LoadZones(region);
            if (highlight)
                mapViewer.ClearHighlightZones();
            zonesList_.Clear();
            int zone_intersect = 0;
            foreach (var zone in region.zones)
            {
                zonesList_.Add(zone);
                if (highlight)
                {
                    if (zone.Error)
                    {
                        mapViewer.HighlightZone(zone, Colors.White);
                    }
                    else if (zone.Warning)
                    {
                        mapViewer.HighlightZone(zone, Colors.Cyan);
                        zone_intersect++;
                    }
                }
            }
            listviewZones.Tag = region;
            if (highlight)
                labelInfo.Text = $"Zones chargées: {zonesList_.Count}, zones avec intersections : {zone_intersect}";
        }

        private void listviewRegions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si edition du nom d'une région en cours alors on bloque le changement de région
            if (listviewRegions.SelectedItem != null && listviewRegions.Tag != null)
            {
                listviewRegions.SelectedItem = listviewRegions.Tag;
                return;
            }
            if (e.AddedItems.Count > 0)
            {
                var region = e.AddedItems[0] as Region;
                LoadZones(region, true); // true => gestion des highlights de zone
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

        private void RegionEditNameMode(Region region, bool value)
        {
            listviewRegions.Tag = (value) ? region : null;
            tabItemDataRegions.IsEnabled = !value;
            tabItemRegions.IsEnabled = !value;
            tabItemZones.IsEnabled = !value;
            mapViewer.IsEnabled = !value;
            region.EditLabel = value;
            if (value == true)
            {
                region.NewLabel = region.Label;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (listviewRegions.SelectedItem != null)
            {
                var region = listviewRegions.SelectedItem as Region;
                if (region != null)
                {
                    var cmd = (sender as MenuItem).Tag as string;
                    switch (cmd)
                    {
                        case "rename":
                            {
                                RegionEditNameMode(region, true);
                                break;
                            }

                        case "delete":
                            {
                                regionsList_.Remove(region);
                                IncSaveCounter();
                                ConsoleWriteLine($"Suppression de la région '{region.Label}'");
                                break;
                            }
                    }
                }
            }
        }

        private void listviewRegions_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var region = listviewRegions.SelectedItem as Region;
            if (region != null && region.EditLabel)
            {
                e.Handled = true;
            }
        }

        private void listviewRegions_KeyDown(object sender, KeyEventArgs e)
        {
            if (lockKeyboard_ != 0) return;
            // Ctrl-C
            lockKeyboard_++;
            var region = listviewRegions.SelectedItem as Region;
            if (region != null)
            {
                if (e.Key == Key.C && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
                {
                    Clipboard.SetText(region.Label);
                    ConsoleWriteLine(region.Label);
                }
                // Del
                else if (e.Key == Key.Delete && region.EditLabel == false)
                {
                    regionsList_.Remove(region);
                    IncSaveCounter();
                    ConsoleWriteLine($"Suppression de la région '{region.Label}'");
                    lockKeyboard_++;
                }
                else if (e.Key == Key.Escape && region.EditLabel == true)
                {
                    RegionEditNameMode(region, false);
                }
                else if (e.Key == Key.Enter && region.EditLabel == true)
                {
                    RegionEditNameMode(region, false);
                    ConsoleWriteLine($"Région '{region.Label}' renommé en '{region.NewLabel}'");
                    region.Label = region.NewLabel;
                    IncSaveCounter();
                }
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var region = listviewRegions.SelectedItem as Region;
            if (region != null && region.EditLabel)
            {
                region.NewLabel = (sender as TextBox).Text;
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
                    IncSaveCounter();
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