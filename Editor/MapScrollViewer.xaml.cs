using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static ARKRegionsEditor.ArkWiki;

namespace ARKRegionsEditor
{
    /// <summary>
    /// Interaction logic for MapScrollViewer.xaml
    /// </summary>
    public partial class MapScrollViewer : UserControl, INotifyPropertyChanged
    {
        string mapName_;
        MapSize mapSize_;
        Point? lastCenterPositionOnTarget_;
        Point lastMousePosition_;
        Point? lastDragPoint_;
        Rect viewArea = new Rect();
        double lastScale_;
        int mouseCaptureMove_;
        Point pingLastPoint;
        int mapBorderWidth_;
        Brush mapBorderColor_;
        Point contextMenuMousePos_;
        List<MapItem> zones_;
        int zonesOpacity_ = 50;
        Color zonesColor_ = Colors.Red;
        Color regionOutilineColor_ = Colors.Yellow;
        bool regionWikiStyle_;
        Region currentRegion_;
        bool editMode_;

        public MapScrollViewer()
        {
            InitializeComponent();
            DataContext = this;

            mapSize_ = new MapSize(0, 0, 100, 100);
            zones_ = new List<MapItem>();

            scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            // In the button-down state, the move.. is processed by capturing the event only in the scroll viewer.
            scrollViewer.MouseMove += ScrollViewer_MouseMove;
            // Button up for capture mode is only handled by the scroll view.
            scrollViewer.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;

            // Button down from any object : Needed to drag map with mouse button
            gridMap.MouseLeftButtonDown += OnMouseLeftButtonDown;
            // just a move that decides the mode
            gridMap.MouseMove += OnMouseMove;

            gridMap.LayoutUpdated += GridMap_LayoutUpdated;

            canvasZones.MouseLeftButtonDown += OnMouseLeftButtonDown;
            canvasZones.MouseMove += OnMouseMoveCanvas;

            canvasRegion.MouseLeftButtonDown += OnMouseLeftButtonDown;
            canvasRegion.MouseMove += OnMouseMoveCanvas;

            ZoomInFull();
        }

        // A la première mise à jour graphique de gridMap on initialise ViewArea
        private void GridMap_LayoutUpdated(object sender, EventArgs e)
        {
            if (gridMap.ActualWidth != 0)
            {
                gridMap.LayoutUpdated -= GridMap_LayoutUpdated;
                ZoomInFull();
            }
        }

        public new FrameworkElement Content
        {
            get => (FrameworkElement)gridMap.Children[0];
            set
            {
                gridMap.Children.Clear();
                gridMap.Children.Add(value);
                Rect view = new Rect(0, 0, value.ActualWidth, value.ActualHeight);
                ViewArea = view;
            }
        }

        public string MapName
        {
            get { return mapName_; }
            set { mapName_ = value; }
        }

        public MapSize MapSize
        {
            get { return mapSize_; }
            set { mapSize_ = value; }
        }

        public string MapImage
        {
            set
            {
                try
                {
                    // Test ressource ou ficheir ?
                    var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(value);
                    if (stream != null)
                    {
                        mapImage.Source = BitmapFrame.Create(stream);
                    }
                    else if (System.IO.File.Exists(value))
                    {
                        mapImage.Source = new BitmapImage(new Uri(value));
                    }
                    else
                    {
                        MessageBox.Show($"la carte {value} n'a pas été trouvée !");
                    }
                }
                catch
                {
                    MessageBox.Show($"la carte {value} n'a pas été trouvée dans les ressources !");
                }
                if (mapSize_ == null)
                {
                    mapSize_ = new MapSize();
                }
                mapSize_.pX = mapBorderWidth_;
                mapSize_.pY = mapBorderWidth_;
                mapSize_.pWidth = (int)mapImage.Source.Width;
                mapSize_.pHeight = (int)mapImage.Source.Height;
                //Console.WriteLine($"Map size = ({mapSize_.pX};{mapSize_.pY})-({mapSize_.pMaxWidth};{mapSize_.pMaxHeight})");
                DrawGrid();
            }
        }

        public int? MapBorderWidth
        {
            get => mapBorderWidth_;
            set
            {
                if (value != null && value != mapBorderWidth_)
                {
                    mapBorderWidth_ = (int)value;
                    mapSize_.pX = mapBorderWidth_;
                    mapSize_.pY = mapBorderWidth_;
                    NotifyPropertyChanged("MapBorderWidth");
                    //Console.WriteLine($"Map size = ({mapSize_.pX};{mapSize_.pY})-({mapSize_.pMaxWidth};{mapSize_.pMaxHeight})");
                }
            }
        }

        public void AutoAdjustBorder()
        {
            var pt = mapSize_.ConvertMapPointToPixel(new MapPos(-2,-2));
            pt.X = -Math.Ceiling(pt.X);
            pt.Y = -Math.Ceiling(pt.Y);
            MapBorderWidth = (int)Math.Max(pt.X, pt.Y);
            DrawGrid();
        }

        public Brush MapBorderColor
        {
            get => mapBorderColor_;
            set
            {
                if (value != mapBorderColor_)
                {
                    mapBorderColor_ = value;
                    NotifyPropertyChanged("MapBorderColor");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public bool EditMode
        {
            get => editMode_;
            set
            {
                // Entrée en mode Edition seulement !
                // Pour la sortie voir la toolbar
                if ((editMode_ == false) && (value == true))
                {
                    editMode_ = true;
                    GridVisibility = true;
                    textboxRegion.Text = currentRegion_.Label;
                    toolbar.Visibility = Visibility.Visible;
                    SendCommand("EnterEditMode");
                    foreach(var zone in zones_)
                    {
                        zone.Save();
                    }
                    DrawRegion();
                }
                else if (value == false)
                {
                    editMode_ = false;
                    toolbar.Visibility = Visibility.Hidden;
                    GridVisibility = false;
                }
            }
        }

        public Region CurrentRegion => currentRegion_;

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            EditMode = false;
            DrawRegion();
            RescaleCanvas(Scale);
            SendCommand("UpdateRegion");
            SendCommand("ExitEditMode");
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            EditMode = false;
            foreach (var zone in zones_)
            {
                zone.CancelEdit();
            }
            DrawRegion();
            RescaleCanvas(Scale);
            SendCommand("ExitEditMode");
        }

        public void Clear()
        {
            MapName = null;
            MapBorderWidth = 0;
            mapSize_ = new MapSize(0, 0, 100, 100);
            mapImage.Source = null;
            ClearZones();
        }

        public void ClearZones()
        {
            canvasRegion.Children.Clear();
            canvasZones.Children.Clear();
            zones_.Clear();
            currentRegion_ = null;
        }

        public void SetZonesOpacity(int value)
        {
            zonesOpacity_ = value;
            foreach (var zone in zones_)
            {
                zone.Opacity = value / 100.0;
            }
        }
        public int ZonesOpacity
        {
            get => zonesOpacity_;
            set => SetZonesOpacity(value);
        }

        public void SetZonesColor(Color value)
        {
            zonesColor_ = value;
            foreach (var zone in zones_)
            {
                zone.Fill = value;
            }
        }
        public Color ZonesColor
        {
            get => zonesColor_;
            set => SetZonesColor(value);
        }

        public void LoadZones(Region region)
        {
            currentRegion_ = region;
            foreach (var zone in region.zones)
            {
                var map_item = new MapItem(this, zone);
                map_item.BuildGeometry(zonesColor_, zonesOpacity_);
                map_item.Rescale(Scale);
                canvasZones.Children.Add(map_item.Canvas);
                zones_.Add(map_item);
            }
            DrawRegion();
            RescaleCanvas(Scale);
        }

        public void AddZone(MapZone zone)
        {
            var map_item = new MapItem(this, zone);
            map_item.BuildGeometry(zonesColor_, zonesOpacity_);
            map_item.Rescale(Scale);
            if (EditMode)
            {
                map_item.Save();
            }
            canvasZones.Children.Add(map_item.Canvas);
            zones_.Add(map_item);
            DrawRegion();
            RescaleCanvas(Scale);
        }

        public void UpdateRegion()
        {
            if (currentRegion_ != null)
            {
                currentRegion_.Update();
                SendCommand("UpdateRegion");
            }
        }

        // Dessine le contour de la region
        protected void DrawRegion(Color color, bool wikiStyle)
        {
            // Patch consigne si dans le mode edition
            if (editMode_)
            {
                color = Colors.Yellow;
                wikiStyle = false;
            }
            canvasRegion.BeginInit();
            canvasRegion.Children.Clear();
            var combinedGeometry = new CombinedGeometry() { GeometryCombineMode = GeometryCombineMode.Union };
            foreach (var zone in zones_)
            {
                combinedGeometry = new CombinedGeometry(combinedGeometry, new RectangleGeometry(zone.Rect));
            }

            if (wikiStyle)
            {
                color = Colors.Red;
            }
            var color_brush = new SolidColorBrush(color);
            // Créer un dessin pour les rayures obliques
            var stripesDrawing = new GeometryDrawing
            {
                Brush = Brushes.Transparent, // Assurez-vous que le fond soit transparent
                Pen = new Pen(color_brush, 1), // Couleur et épaisseur des rayures
                // Ajouter une géométrie pour les rayures obliques (ligne)
                Geometry = new LineGeometry(new Point(0, 0), new Point(10, 10))
            };

            // Créer le DrawingBrush avec le dessin de rayures
            var stripesBrush = new DrawingBrush
            {
                Drawing = stripesDrawing,
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 10, 10),
                ViewportUnits = BrushMappingMode.Absolute
            };

            Path path = new Path
            {
                Data = combinedGeometry,
                Stroke = color_brush,
                StrokeThickness = 2,
                Fill = wikiStyle == true ? color_brush : stripesBrush,
                Opacity = wikiStyle == true ? 0.7 : 1
            };
            if (wikiStyle)
            {
                var blurEffect = new BlurEffect
                {
                    Radius = 20
                };
                path.Effect = blurEffect;
            }
            canvasRegion.Children.Add(path);
            canvasRegion.EndInit();
        }

        public void DrawRegion()
        {
            DrawRegion(regionOutilineColor_, regionWikiStyle_);
        }

        public bool? RegionVisibility
        {
            get => canvasRegion.Visibility == Visibility.Visible;
            set => canvasRegion.Visibility = (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool? ZonesVisibility
        {
            get => canvasZones.Visibility == Visibility.Visible;
            set => canvasZones.Visibility = (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool? RegionWikiStyle
        {
            get => regionWikiStyle_;
            set
            {
                if (value != regionWikiStyle_)
                {
                    regionWikiStyle_ = (bool)value;
                    DrawRegion();
                }
            }
        }

        public bool CheckZoneEditMode(MapItem mapItem)
        {
            foreach (var item in zones_)
            {
                if (item != mapItem)
                {
                    if (item.EditMode)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void ClearHighlightZones()
        {
            canvasZones.BeginInit();
            foreach (var item in zones_)
            {
                item.ClearHightlight();
            }
            canvasZones.EndInit();
        }

        public void HighlightZone(MapZone zone, Color color)
        {
            canvasZones.BeginInit();
            foreach (var item in zones_)
            {
                if (item.Zone == zone)
                {
                    item.Highlight(color);
                    // Move at the top of Z (use this because SetZIndex doesn't work)
                    int pos = canvasZones.Children.IndexOf(item.Canvas);
                    canvasZones.Children.RemoveAt(pos);
                    canvasZones.Children.Add(item.Canvas);
                    break;
                }
            }
            canvasZones.EndInit();
        }

        protected void DrawGrid()
        {
            canvasGrid.Visibility = Visibility.Collapsed;
            canvasGrid.BeginInit();
            canvasGrid.Children.Clear();

            for (int pos = 0; pos < 100; pos++)
            {
                var o = mapSize_.ConvertMapPointToPixel(new MapPos(pos, pos));
                var line_vertical = new Line()
                {
                    Stroke = new SolidColorBrush(Colors.Silver),
                    StrokeThickness = 1,
                    Opacity = 0.5,
                    X1 = 0,
                    Y1 = o.Y,
                    X2 = mapSize_.pMaxWidth,
                    Y2 = o.Y
                };
                canvasGrid.Children.Add(line_vertical);
                var line_horizontal = new Line()
                {
                    Stroke = new SolidColorBrush(Colors.Silver),
                    StrokeThickness = 1,
                    Opacity = 0.5,
                    X1 = o.X,
                    Y1 = 0,
                    X2 = o.X,
                    Y2 = mapSize_.pMaxHeight
                };
                canvasGrid.Children.Add(line_horizontal);
            }
            canvasGrid.EndInit();
        }

        public bool GridVisibility
        {
            get => canvasGrid.Visibility == Visibility.Visible;
            set => canvasGrid.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RescaleCanvas(double scale)
        {
            foreach (var item in zones_)
            {
                item.Rescale(scale);
            }
            DrawRegion();
            if (pingEllipse.Tag != null)
            {
                if (pingEllipse.Tag == this && pingLastPoint != null)
                {
                    RescalePing(20, pingLastPoint);
                }
                else
                {
                    //(var pos, var size) = (pingEllipse.Tag as MapPoi).CurrentPosAndSize;
                    //RescalePing(size, pos);
                }
            }
        }

        protected void PrintInfo(MouseEventArgs e)
        {
            var va = new Point(ViewArea.X, ViewArea.Y);
            var p0 = new Point(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
            var mgc = e?.GetPosition(gridMap) ?? new Point(0, 0);
            var mpt = mapSize_.ConvertPixelPointToMap(mgc.X, mgc.Y);
#if false //DEBUG
            //labelInfo.Text = $"P=({(int)mgc.X};{(int)mgc.Y}) M=({mpt.lat:0.00};{mpt.lon:0.00}) VA=({(int)va.X};{(int)va.Y}) ({(int)p0.X};{(int)p0.Y}) S={Scale:0.0000}";
            labelInfo.Text = $"Pixels=({(int)mgc.X};{(int)mgc.Y}) MapPos=(lat {mpt.lat:0.00};lon {mpt.lon:0.00}) Scale={Scale:0.0000}";
#else
            labelInfo.Text = $"Lat {mpt.lat:00.00}, Lon {mpt.lon:00.00}";
#endif
        }

        public double MaxScale
        {
            get; set;
        }

        public double Scale
        {
            get => scaleTransform.ScaleX;
            set
            {
                if (value == double.NaN || value < 0)
                {
                    value = 1;
                }
                scaleTransform.ScaleX = value;
                scaleTransform.ScaleY = value;
                if (lastScale_ != value)
                {
                    lastScale_ = value;
                    RescaleCanvas(value);
                }
            }
        }

        public Point Origin
        {
            set
            {
                scrollViewer.ScrollToHorizontalOffset(value.X * Scale);
                scrollViewer.ScrollToVerticalOffset(value.Y * Scale);
            }
            get
            {
                return new Point(ViewArea.X, ViewArea.Y);
            }
        }

        public Rect ViewArea
        {
            set
            {
                double windowWidth = scrollViewer.ViewportWidth;
                double windowHeight = scrollViewer.ViewportHeight;
                double windowRate = windowWidth / windowHeight;

                if (windowWidth == 0)
                {
                    windowWidth = scrollViewer.ActualWidth;
                    windowHeight = scrollViewer.ActualHeight;
                }

                double a = gridMap.Width;

                double contentWidth = gridMap.ActualWidth; // grid
                double contentHeight = gridMap.ActualHeight; // grid
                double contentRate = contentWidth / contentHeight;

                //oriented in content.
                Rect rect = value;

                if (rect.Width == 0 || contentWidth == 0 || windowWidth == 0)
                {
                    viewArea = rect;
                    return;
                }

                //--decide scale
                //allowed by scrollViewer
                double minScale = Math.Min(windowWidth / contentWidth, windowHeight / contentHeight);

                double scaleX = Math.Max(windowWidth / rect.Width, minScale);
                double scaleY = Math.Max(windowHeight / rect.Height, minScale);

                double scale;
                //(x or y) axis should be extended.
                if (scaleX > scaleY)
                {
                    scale = scaleY;
                    double oldWidth = rect.Width;
                    rect.Width = windowWidth / scale;
                    rect.X -= (rect.Width - oldWidth) / 2;//extend from center
                }
                else
                {
                    scale = scaleX;
                    double oldHeight = rect.Height;
                    rect.Height = windowHeight / scale;
                    rect.Y -= (rect.Height - oldHeight) / 2;
                }
                Scale = scale;

                scrollViewer.ScrollToHorizontalOffset(rect.X * scale);
                scrollViewer.ScrollToVerticalOffset(rect.Y * scale);

                PrintInfo(null);
            }

            get
            {
                return viewArea;
            }
        }

        public void ZoomInFull()
        {
            if (gridMap.ActualWidth != 0)
            {
                ViewArea = new Rect(0, 0, gridMap.ActualWidth, gridMap.ActualHeight);
            }
        }

        public void ZoomTo(double left, double top)
        {
            if (left != double.NaN && top != double.NaN)
            {
                scrollViewer.ScrollToHorizontalOffset(left - (scrollViewer.ActualWidth / 2));
                scrollViewer.ScrollToVerticalOffset(top - (scrollViewer.ActualHeight / 2));
            }
        }

        public void Ping(double width, Point pos, object tag, Color color)
        {
            // Scale 1 => 50
            // Scale 8 => 100
            pingEllipse.Stroke = new SolidColorBrush(color);
            pingEllipse.Width = 42.86 + 7.14 * Scale;
            pingEllipse.Height = pingEllipse.Width;
            Canvas.SetLeft(pingEllipse, pos.X + width / 2 - pingEllipse.Width / 2);
            Canvas.SetTop(pingEllipse, pos.Y + width / 2 - pingEllipse.Height / 2);
            pingEllipse.Tag = tag;
            pingEllipse.Visibility = Visibility.Visible;
            pingStoryboard.Storyboard.Stop();
            pingStoryboard.Storyboard.Begin();
        }

        public void RescalePing(double width, Point pos)
        {
            pingEllipse.Width = 42.86 + 7.14 * Scale;
            pingEllipse.Height = pingEllipse.Width;
            Canvas.SetLeft(pingEllipse, pos.X + width / 2 - pingEllipse.Width / 2);
            Canvas.SetTop(pingEllipse, pos.Y + width / 2 - pingEllipse.Height / 2);
        }

        public void ZoomToMapPos(MapPos mapPos, bool ping, Color color)
        {
            pingStoryboard.Storyboard.Stop();
            var pos = MapSize.ConvertMapPointToPixel(mapPos);
            pingLastPoint.X = pos.X * Scale - 10;
            pingLastPoint.Y = pos.Y * Scale - 10;
            ZoomTo(pingLastPoint.X, pingLastPoint.Y);
            if (ping)
            {
                Ping(20, pingLastPoint, this, color);
            }
        }

        private void ComputeMouseMove(object sender, MouseEventArgs e)
        {
            if (lastDragPoint_.HasValue)
            {
                Point posNow = e.GetPosition(scrollViewer);

                double dX = posNow.X - lastDragPoint_.Value.X;
                double dY = posNow.Y - lastDragPoint_.Value.Y;

                lastDragPoint_ = posNow;

                Rect rect = ViewArea;
                rect.X -= dX / Scale;
                rect.Y -= dY / Scale;
                ViewArea = rect;
                mouseCaptureMove_++;
            }
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            ComputeMouseMove(sender, e);
            PrintInfo(e);
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            ComputeMouseMove(sender, e);
            lastMousePosition_ = e.GetPosition(gridMap);
        }
        void OnMouseMoveCanvas(object sender, MouseEventArgs e)
        {
            ComputeMouseMove(sender, e);
            lastMousePosition_ = e.GetPosition(gridMap);
        }
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var mousePos = e.GetPosition(scrollViewer);
            if (mousePos.X <= scrollViewer.ViewportWidth && mousePos.Y <
                scrollViewer.ViewportHeight) //make sure we still can use the scrollbars
            {
                lastDragPoint_ = mousePos;
                Mouse.Capture(scrollViewer);
            }
            mouseCaptureMove_ = 0;
#if false
            // Process clic
            var src = e.Source as FrameworkElement;
            if (src?.Tag == null) // Si pas de Tag alors clic dans une zone vide
            {
            }
#endif
        }
        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollViewer.ReleaseMouseCapture();
            lastDragPoint_ = null;
            mouseCaptureMove_ = 0;
        }

        void ComputeScale(double deltaScale, Point pos)
        {
            Rect view = ViewArea;

            double nuWidth = view.Width * deltaScale;
            double nuHeight = view.Height * deltaScale;

            // check scaling max
            double scale = scrollViewer.ViewportWidth / nuWidth;
            if (scale > MaxScale)
            {
                nuWidth = scrollViewer.ViewportWidth / MaxScale;
                nuHeight = scrollViewer.ViewportHeight / MaxScale;
            }

            // leftSide / total width
            double rateX = (pos.X - view.X) / view.Width;
            view.X -= (nuWidth - view.Width) * rateX;

            //topSide / total height
            double rateY = (pos.Y - view.Y) / view.Height;
            view.Y -= (nuHeight - view.Height) * rateY;

            view.Width = nuWidth;
            view.Height = nuHeight;

            ViewArea = view;
        }

        void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double delta_scale = 1;
            if (e.Delta > 0)
            {
                delta_scale /= 2;
            }
            if (e.Delta < 0)
            {
                delta_scale *= 2;
            }
            if (delta_scale > MaxScale)
            {
                delta_scale = MaxScale;
            }
            ComputeScale(delta_scale, e.GetPosition(gridMap));
        }

        void OnSliderValueChanged(object sender,
             RoutedPropertyChangedEventArgs<double> e)
        {
            Scale = e.NewValue;

            var centerOfViewport = new Point(scrollViewer.ViewportWidth / 2,
                                             scrollViewer.ViewportHeight / 2);
            lastCenterPositionOnTarget_ = scrollViewer.TranslatePoint(centerOfViewport, gridMap); // grid
        }

        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            double scale = Scale;
            if (scale != 0)
            {
                viewArea.X = scrollViewer.HorizontalOffset / scale;
                viewArea.Y = scrollViewer.VerticalOffset / scale;
                viewArea.Width = scrollViewer.ViewportWidth / scale;
                viewArea.Height = scrollViewer.ViewportHeight / scale;

                double contentWidth = gridMap.ActualWidth;
                double contentHeight = gridMap.ActualHeight;

                if (viewArea.Width > contentWidth)
                {
                    viewArea.X -= (viewArea.Width - contentWidth) / 2;
                }

                if (viewArea.Height > contentHeight)
                {
                    viewArea.Y -= (viewArea.Height - contentHeight) / 2;
                }
            }
        }

        private void Storyboard_Completed(object sender, EventArgs e)
        {
            pingEllipse.Visibility = Visibility.Collapsed;
            pingEllipse.Tag = null;
        }

        private void gridMap_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            contextMenuMousePos_ = lastMousePosition_;
        }

        // Command event
        public delegate void CommandEventHandler(object sender, CommandEventArgs e);
        public event CommandEventHandler CommandEvent;
        public void SendCommand(string command, object obj = null)
        {
            CommandEvent?.Invoke(this, new CommandEventArgs(command, currentRegion_, obj));
        }
    }

    public class CommandEventArgs : EventArgs
    {
        private string command_;
        private Region region_;
        private object object_;

        public string Command => command_;
        public Region Region => region_;
        public object Object => object_;

        public CommandEventArgs(string command, Region region, object obj=null)
        {
            this.command_ = command;
            this.region_ = region;
            this.object_ = obj;
        }
    }

    public class MapBorder
    {
        public double Lon;
        public double Lat;
    }

    public class MapSize
    {
        public double cLon; // c => coordonnées
        public double cLat;
        public double cWidth;
        public double cHeight;
        public int pX;
        public int pY;
        public int pWidth; // pixel width
        public int pHeight; // pixel width

        public double cMaxLat => cHeight + cLat;
        public double cMaxLon => cWidth + cLon;
        public int pMaxWidth => pWidth + (2 * pX);
        public int pMaxHeight => pHeight + (2 * pY);

        public MapSize()
        {
            pX = 0;
            pY = 0;
            cLon = 0;
            cLat = 0;
            cWidth = 100 - cLon;
            cHeight = 100 - cLat;
        }

        public MapSize(double x1, double y1, double x2, double y2)
        {
            pX = 0;
            pY = 0;
            cLon = x1;
            cLat = y1;
            cWidth = x2 - cLon;
            cHeight = y2 - cLat;
        }

        public Point ConvertMapPointToPixel(MapPos pos)
        {
            if (pos != null && pWidth != 0 && pHeight != 0 && cWidth != 0 && cHeight != 0)
            {
                double o_x = pX + ((pos.lon - cLon) * pWidth) / cWidth;
                double o_y = pY + ((pos.lat - cLat) * pHeight) / cHeight;
                return new Point(o_x, o_y);
            }
            return new Point(0, 0);
        }

        public MapPos ConvertPixelPointToMap(double x, double y)
        {
            if (pWidth != 0 && pHeight != 0)
            {
                double o_lon = ((cWidth * (x - pX)) / pWidth) + cLon;
                double o_lat = ((cHeight * (y - pY)) / pHeight) + cLat;
                return new MapPos(o_lat, o_lon);
            }
            return new MapPos(0, 0);
        }

        public new bool Equals(object obj)
        {
            if (obj == null || !(obj is MapSize))
                return false;

            var objet2 = (MapSize)obj;
            return this.cLon == objet2.cLon && this.cLat == objet2.cLat && this.cWidth == objet2.cWidth && this.cHeight == objet2.cHeight;
        }

        public override string ToString()
        {
            return $"({cLon:F2};{cLat:F2};{cWidth:F2};{cHeight:F2})";
        }
    }

    public class IntPos
    {
        public int Lat { get; set; }
        public int Lon { get; set; }

        public IntPos(float lat, float lon)
        {
            // test avec 3 digits après la virgule
            this.Lat = (int)(1000 * lat);
            this.Lon = (int)(1000 * lon);
        }

        public bool Equals(IntPos src)
        {
            return src.Lat == this.Lat && src.Lon == this.Lon;
        }
    }

    public struct MapPosStruct
    {
        public float lat;
        public float lon;
    }

    public class MapPos
    {
        public float lat;
        public float lon;
        public MapPos(ArkWikiJsonMarker marker)
        {
            lat = marker.lat;
            lon = marker.lon;
        }
        public MapPos(MapPosStruct pos)
        {
            lat = pos.lat;
            lon = pos.lon;
        }
        public MapPos(float lat, float lon)
        {
            this.lat = lat;
            this.lon = lon;
        }
        public MapPos(double lat, double lon)
        {
            this.lat = (float)lat;
            this.lon = (float)lon;
        }

        public MapPos(ObeliskJsonBiomePoint point, ArkWikiJsonRect borders)
        {
            lat = Saturation((float)Math.Round(point.lat), borders.top, borders.bottom);
            lon = Saturation((float)Math.Round(point.lon), borders.left, borders.right);
        }

        public bool Equals(MapPos src)
        {
            var isrc = new IntPos(src.lat, src.lon);
            var ithis = new IntPos(lat, lon);
            return isrc.Equals(ithis);
        }

        private float Saturation(float value, float min, float max)
        {
            if (value < min) return min;
            else if (value > max) return max;
            return value;
        }
    }
}