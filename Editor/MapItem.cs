using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Data;

namespace ARKRegionsEditor
{
    // Specify egdes in absolute map pos (lat / lon)
    internal struct Edges
    {
        internal MapPosStruct topLeft;
        internal MapPosStruct bottomRight;
    }
    internal struct EdgesPixels
    {
        internal double left;
        internal double top;
        internal double right;
        internal double bottom;
    }

    public class MapItem
    {
        private double scale_;
        private MapScrollViewer map_;
        private bool editionMode_;
        private Point posA_;
        private Point posB_;
        private Rectangle highlight_;
        private Line[] edges_;
        private Rectangle rect_;
        private Edges edgeEdit_;
        protected ContextMenu menu_;
        private MapZone zoneSave_;
        private SolidColorBrush rectColor_;

        public Canvas Canvas;
        public MapZone Zone;

        protected const int Top = 0;
        protected const int Right = 1;
        protected const int Bottom = 2;
        protected const int Left = 3;

        public MapItem(MapScrollViewer map, MapZone zone)
        {
            this.map_ = map;
            this.Zone = zone;
            scale_ = map.Scale;
        }

        public void BuildGeometry(Color color, int opacity)
        {
            rectColor_ = new SolidColorBrush(color);
            // Main geometry
            rect_ = new Rectangle()
            {
                Fill = rectColor_,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 2, 4 },
                Opacity = opacity / 100.0,
                Cursor = Cursors.Hand
            };
            rect_.MouseLeftButtonDown += MouseLeftButtonDown;
            highlight_ = new Rectangle()
            {
                Opacity = 1
            };
            Canvas = new Canvas();
            Canvas.Children.Add(rect_);
            Canvas.Children.Add(highlight_);

            // Edges
            edges_ = new Line[4];
            for (int i = 0; i < 4; i++)
            {
                edges_[i] = new Line()
                {
                    Stroke = new SolidColorBrush(Colors.Yellow),
                    StrokeThickness = 4,
                    Visibility = Visibility.Collapsed,
                    Tag = i
                };
            }
            edges_[0].Cursor = Cursors.SizeNS; // Top
            edges_[1].Cursor = Cursors.SizeWE; // Right
            edges_[2].Cursor = Cursors.SizeNS; // Bottom
            edges_[3].Cursor = Cursors.SizeWE; // Left

            foreach (var edge in edges_)
            {
                edge.MouseLeftButtonDown += Edge_MouseLeftButtonDown;
                edge.MouseLeftButtonUp += Edge_MouseLeftButtonUp;
                edge.MouseMove += Edge_MouseMove;
                Canvas.Children.Add(edge);
            }

            // Menu
            menu_ = new ContextMenu();
            menu_.Items.Add(new MenuItem()
            {
                Header = "Valider",
                Tag = "valid",
            });
            menu_.Items.Add(new MenuItem()
            {
                Header = "Annuler",
                Tag = "cancel",
            });
            menu_.Items.Add(new MenuItem()
            {
                Header = "Copier",
                Tag = "copy",
            });
            menu_.Items.Add(new MenuItem()
            {
                Header = "Supprimer",
                Tag = "delete",
            });
            foreach (MenuItem item in menu_.Items)
            {
                item.Click += Menu_Click;
            }
            rect_.ContextMenu = menu_;
            rect_.ContextMenuOpening += Rect_ContextMenuOpening;
        }

        public double Opacity
        {
            set => rect_.Opacity = value;
        }

        public Color Fill
        {
            set => rect_.Fill = new SolidColorBrush(value);
        }

        public bool EditMode => editionMode_;

        public Rect Rect
        {
            get
            {
                // si map mode edition alors prendre les infos originales
                if (map_.EditMode)
                {
                    (var posA, var posB) = ComputePosAB(zoneSave_);
                    return new Rect(posA.X * scale_, posA.Y * scale_, (posB.X - posA.X) * scale_, (posB.Y - posA.Y) * scale_);
                }
                else
                {
                    return new Rect(posA_.X * scale_, posA_.Y * scale_, rect_.Width, rect_.Height);
                }
            }
        }

        // Sauvegarde la position de la zone (utilisé à l'entrée de la map en mode édtion)
        public void Save()
        {
            zoneSave_ = new MapZone();
            zoneSave_.CopyFrom(Zone);
        }

        public void ClearHightlight()
        {
            if (editionMode_ == true)
            {
                map_.SendCommand("StopEditZone", Zone);
                editionMode_ = false;
            }
            highlight_.Visibility = Visibility.Collapsed;
            foreach (var edge in edges_)
                edge.Visibility = Visibility.Collapsed;
        }

        public void Highlight(Color color)
        {
            if (color == Colors.Transparent) // hide
            {
                highlight_.Visibility = Visibility.Collapsed;
            }
            else
            {
                highlight_.Stroke = new SolidColorBrush(color);
                highlight_.StrokeThickness = 3;
                highlight_.StrokeDashArray = new DoubleCollection() { 2 };
                highlight_.Visibility = Visibility.Visible;
            }
        }

        public void RescaleEdges()
        {
            var tp_lf_map = map_.MapSize.ConvertMapPointToPixel(new MapPos(edgeEdit_.topLeft));
            var bm_rg_map = map_.MapSize.ConvertMapPointToPixel(new MapPos(edgeEdit_.bottomRight));

            EdgesPixels edges;
            edges.top = (tp_lf_map.Y - posA_.Y) * scale_;
            edges.left = (tp_lf_map.X - posA_.X) * scale_;
            edges.bottom = (bm_rg_map.Y - posA_.Y) * scale_;
            edges.right = (bm_rg_map.X - posA_.X) * scale_;

            edges_[Top].X1 = edges.left;
            edges_[Top].Y1 = edges.top;
            edges_[Top].X2 = edges.right;
            edges_[Top].Y2 = edges.top;

            edges_[Bottom].X1 = edges.left;
            edges_[Bottom].Y1 = edges.bottom;
            edges_[Bottom].X2 = edges.right;
            edges_[Bottom].Y2 = edges.bottom;

            edges_[Left].X1 = edges.left;
            edges_[Left].Y1 = edges.top;
            edges_[Left].X2 = edges.left;
            edges_[Left].Y2 = edges.bottom;

            edges_[Right].X1 = edges.right;
            edges_[Right].Y1 = edges.top;
            edges_[Right].X2 = edges.right;
            edges_[Right].Y2 = edges.bottom;
        }

        protected (Point, Point) ComputePosAB(MapZone zn)
        {
            var posA = map_.MapSize.ConvertMapPointToPixel(zn.Pos);
            var posB = map_.MapSize.ConvertMapPointToPixel(new MapPos(zn.Pos.lat + zn.LatLength, zn.Pos.lon + zn.LonLength));
            return (posA, posB);
        }

        // Rename to Update ?
        public void Rescale(double scale)
        {
            scale_ = scale;
            (posA_, posB_) = ComputePosAB(Zone);

            rect_.Width = (posB_.X - posA_.X) * scale;
            rect_.Height = (posB_.Y - posA_.Y) * scale;

            highlight_.Width = rect_.Width;
            highlight_.Height = rect_.Height;

            if (editionMode_ == true)
                RescaleEdges();

            rect_.Fill = Zone.MarkToDelete ? new SolidColorBrush(Colors.Gray) : rectColor_;

            Canvas.SetLeft(Canvas, posA_.X * scale);
            Canvas.SetTop(Canvas, posA_.Y * scale);
        }

        // Enter Edit mode
        protected void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1 && editionMode_ == false)
            {
                // Vérifier si une autre zone n'est pas en édition
                if (map_.CheckZoneEditMode(this) == false)
                {
                    map_.EditMode = true; // on passe la carte en mode edition, la sortie ne peux se faire qu'avec les boutons de la toolbar de la carte

                    map_.ClearHighlightZones();
                    map_.HighlightZone(Zone, Colors.Magenta);

                    edgeEdit_.topLeft.lat = Zone.Pos.lat;
                    edgeEdit_.topLeft.lon = Zone.Pos.lon;
                    edgeEdit_.bottomRight.lat = Zone.Pos.lat + Zone.LatLength;
                    edgeEdit_.bottomRight.lon = Zone.Pos.lon + Zone.LonLength;

                    foreach (var edge in edges_)
                        edge.Visibility = Visibility.Visible;
                    editionMode_ = true;
                    RescaleEdges();
                    map_.SendCommand("StartEditZone", Zone);
                }
            }
            else if (e.ClickCount == 1)
            {
                // Vérifier si une autre zone n'est pas en édition
                if (map_.CheckZoneEditMode(this) == false)
                {
                    map_.SendCommand("SelectZone", Zone);
                }
            }
        }

        protected void Edge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (editionMode_ == true)
            {
                int index = (int)(sender as Line).Tag;
                edges_[index].CaptureMouse();
                e.Handled = true;
            }
        }

        protected void Edge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            int index = (int)(sender as Line).Tag;
            if (edges_[index].IsMouseCaptured)
            {
                edges_[index].ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Edge_MouseMove(object sender, MouseEventArgs e)
        {
            int index = (int)(sender as Line).Tag;
            if (edges_[index].IsMouseCaptured)
            {
                // pixM = mouse pos in grid
                var pixM = e.GetPosition(map_.gridMap);
                // mapM = mouse pos in map
                var mapM = map_.MapSize.ConvertPixelPointToMap(pixM.X, pixM.Y);
                // snap to map grid
                mapM.lat = (float)Math.Round(mapM.lat);
                mapM.lon = (float)Math.Round(mapM.lon);

                // Move only dynamic lines
                switch (index)
                {
                    case Top:
                        {
                            edgeEdit_.topLeft.lat = mapM.lat;
                            break;
                        }
                    case Left:
                        {
                            edgeEdit_.topLeft.lon = mapM.lon;
                            break;
                        }
                    case Bottom:
                        {
                            edgeEdit_.bottomRight.lat = mapM.lat;
                            break;
                        }
                    case Right:
                        {
                            edgeEdit_.bottomRight.lon = mapM.lon;
                            break;
                        }
                }
                RescaleEdges();
                e.Handled = true;
            }
        }

        private void Rect_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (editionMode_ == true)
            {
                e.Handled = false;
                var allItems = menu_.Items.Cast<MenuItem>().ToArray();
                var item = allItems.FirstOrDefault(x => (x.Tag as string) == "delete");
                if (Zone.MarkToDelete)
                {
                    item.IsEnabled = false;
                }
                else
                {
                    item.IsEnabled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        public void CancelEdit()
        {
            ClearHightlight(); // fin du mode edition de la zone
            // Attention NE PAS DETRUIRE OU MODFIER LA REFERENCE DE CET OBJET !
            // cet objet est référencé dans la region à laquelle il appartient
            Zone.CopyFrom(zoneSave_);
            Zone.MarkToDelete = false;
        }

        private void CheckPoint(ref MapPosStruct posA, ref MapPosStruct posB)
        {
            if (posB.lon < posA.lon)
            {
                (posA.lon, posB.lon) = (posB.lon, posA.lon);
            }
            if (posB.lat < posA.lat)
            {
                (posA.lat, posB.lat) = (posB.lat, posA.lat);
            }
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            ClearHightlight(); // fin du mode edition de la zone
            string cmd = (sender as MenuItem).Tag as string;
            switch (cmd)
            {
                case "valid":
                    {
                        // Check inversion par l'edition
                        CheckPoint(ref edgeEdit_.topLeft, ref edgeEdit_.bottomRight);
                        Zone.Lat = edgeEdit_.topLeft.lat;
                        Zone.Lon = edgeEdit_.topLeft.lon;
                        Zone.LatLength = edgeEdit_.bottomRight.lat - edgeEdit_.topLeft.lat;
                        Zone.LonLength = edgeEdit_.bottomRight.lon - edgeEdit_.topLeft.lon;
                        Rescale(scale_);
                        break;
                    }

                case "cancel":
                    {
                        if (Zone.MarkToDelete)
                        {
                            Zone.MarkToDelete = false;
                            Zone.Update();
                            Rescale(map_.Scale);
                        }
                        break;
                    }

                case "delete":
                    {
                        Zone.MarkToDelete = true;
                        Zone.Update();
                        Rescale(map_.Scale);
                        break;
                    }

                case "copy":
                    {
                        map_.SendCommand("CopyZone", Zone);
                        break;
                    }
            }
        }
    }
}
