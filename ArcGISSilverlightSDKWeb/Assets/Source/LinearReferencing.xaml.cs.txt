using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Symbols;

namespace ArcGISSilverlightSDK
{
    public partial class LinearReferencing : UserControl
    {
        private readonly Draw _drawObject;
        readonly GraphicsLayer _graphicsLayer;
        private Polyline _polyline;

        public LinearReferencing()
        {
            InitializeComponent();
            DataContext = this;

            _graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            _polyline = _graphicsLayer.Graphics.Select(g => g.Geometry).OfType<Polyline>().FirstOrDefault();

            _drawObject = new Draw(MyMap)
            {
                LineSymbol = LayoutRoot.Resources["DrawLineSymbol"] as LineSymbol,
            };
            _drawObject.DrawComplete += (s, e) =>
            {
                _polyline = e.Geometry as Polyline;
                _graphicsLayer.ClearGraphics();
                _graphicsLayer.Graphics.Add(new Graphic
                {
                    Geometry = e.Geometry,
                    Symbol = LayoutRoot.Resources["DefaultLineSymbol"] as Symbol,
                });
                UpdateLinearPositions();
            };
            Ratio1 = 0.3;
            Ratio2 = 0.7;
        }

        #region Dependency properties : M1, M2, Ratio1, Ratio2
        // Dependency Property M1 Position
        public double M1
        {
            get { return (double)GetValue(M1Property); }
            set { SetValue(M1Property, value); }
        }

        public static readonly DependencyProperty M1Property =
            DependencyProperty.Register("M1", typeof(double), typeof(LinearReferencing), null);

        // Dependency Property Ratio1 Position
        public double Ratio1
        {
            get { return (double)GetValue(Ratio1Property); }
            set { SetValue(Ratio1Property, value); }
        }

        public static readonly DependencyProperty Ratio1Property =
            DependencyProperty.Register("Ratio1", typeof(double), typeof(LinearReferencing), new PropertyMetadata(RatioPropertyChanged));

        private static void RatioPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LinearReferencing) d).UpdateLinearPositions();
        }

        // Dependency Property M2 Position
        public double M2
        {
            get { return (double)GetValue(M2Property); }
            set { SetValue(M2Property, value); }
        }

        public static readonly DependencyProperty M2Property =
            DependencyProperty.Register("M2", typeof(double), typeof(LinearReferencing), null);

        // Dependency Property Ratio2 Position
        public double Ratio2
        {
            get { return (double)GetValue(Ratio2Property); }
            set { SetValue(Ratio2Property, value); }
        }

        public static readonly DependencyProperty Ratio2Property =
            DependencyProperty.Register("Ratio2", typeof(double), typeof(LinearReferencing), new PropertyMetadata(RatioPropertyChanged));
        #endregion

        private void UnSelectTools()
        {
            foreach (UIElement element in MyStackPanel.Children)
                if (element is Button)
                    VisualStateManager.GoToState((element as Button), "UnSelected", false);
        }

        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            UnSelectTools();

            VisualStateManager.GoToState(sender as Button, "Selected", false);

            switch (((Button) sender).Tag as string)
            {
                case "DrawPolyline":
                    _drawObject.DrawMode = DrawMode.Polyline;
                    break;
                case "DrawlineSegment":
                    _drawObject.DrawMode = DrawMode.LineSegment;
                    break;
                case "DrawFreehand":
                    _drawObject.DrawMode = DrawMode.Freehand;
                    break;
                default:
                    _drawObject.DrawMode = DrawMode.None;
                    _polyline = null;
                    _graphicsLayer.ClearGraphics();
                    UpdateLinearPositions();
                    break;
            }
            _drawObject.IsEnabled = (_drawObject.DrawMode != DrawMode.None);
        }

        // Update LinearPositions from the Ratios
        private void UpdateLinearPositions()
        {
            M1 = _polyline == null ? double.NaN : Ratio1 * _polyline.Length();
            M2 = _polyline == null ? double.NaN : Ratio2 * _polyline.Length();
            UpdateGraphics();
        }

        private void UpdateGraphics()
        {
            if (_polyline != null)
            {
                var point1 = _polyline.LinearPosition(M1);
                var point2 = _polyline.LinearPosition(M2);
                var subline = _polyline.M1ToM2(M1, M2);
                if (_graphicsLayer.Graphics.Count >= 4)
                {
                    // reuse existing graphics
                    _graphicsLayer.Graphics[1].Geometry = subline;
                    _graphicsLayer.Graphics[2].Geometry = point1;
                    _graphicsLayer.Graphics[3].Geometry = point2;
                }
                else
                {
                    // Create new graphics
                    _graphicsLayer.Graphics.Add(new Graphic
                    {
                        Symbol = LayoutRoot.Resources["LineSymbol"] as Symbol,
                        Geometry = subline
                    });
                    _graphicsLayer.Graphics.Add(new Graphic
                    {
                        Symbol = LayoutRoot.Resources["MarkerSymbol"] as Symbol,
                        Geometry = point1
                    });
                    _graphicsLayer.Graphics.Add(new Graphic
                    {
                        Symbol = LayoutRoot.Resources["MarkerSymbol"] as Symbol,
                        Geometry = point2
                    });
                }
            }
        }
    }

    /// <summary>
    /// LinearReferencing Extension class:
    ///   - calculate Polyline Length
    ///   - calculate a Linear Position along a polyline
    ///   - extract the polyline between two linear positions
    /// </summary>
    public static class LinearReferencingExtension
    {
        #region M1ToM2
        // Extract polyline between two Linear positions 
        public static Polyline M1ToM2(this Polyline polyline, double m1, double m2)
        {
            if (m1 > m2)
            {
                var tmp = m2;
                m2 = m1;
                m1 = tmp;
            }
            var length = polyline.Length();
            if (m2 < 0 || m1 > length)
                return null;

            // Get the paths needed to cover M1-M2
            var mSelector = new MSelector(m1, m2);
            var paths = polyline.Clone().Paths.Where(path => mSelector.Step(path.Length())).ToArray();
            if (!paths.Any())
                return null;

            // cut end of the last path
            if (!mSelector.M2Overflow)
                paths[paths.Length - 1] = M1ToM2(paths.Last(), 0, mSelector.M2);

            // cut start of the first path
            paths[0] = M1ToM2(paths.First(), mSelector.M1, double.PositiveInfinity);

            return new Polyline
            {
                Paths = new ObservableCollection<PointCollection>(paths)
            };
        }

        private static PointCollection M1ToM2(IEnumerable<MapPoint> points, double m1, double m2)
        {
            // Get the segments needed to cover M1-M2
            var mSelector = new MSelector(m1, m2);
            var segments = points.ToSegments().Where(seg => mSelector.Step(seg.Length())).ToArray();
            if (!mSelector.M2Overflow)
            {
                var lastSeg = segments.Last();
                lastSeg.Point2 = lastSeg.LinearPosition(mSelector.M2); // cut end of last segment
            }
            var firstSeg = segments.First();
            firstSeg.Point1 = firstSeg.LinearPosition(mSelector.M1); // cut start of first segment

            return segments.ToPointCollection();
        }
        #endregion

        #region LinearPosition
        // Get a Linear Position on a multipath polyline
        public static MapPoint LinearPosition(this Polyline polyline, double m)
        {
            var mSelector = new MSelector(m, m);
            PointCollection path1 = polyline.Paths.FirstOrDefault(path => mSelector.Step(path.Length()));
            return path1 == null ? polyline.Paths.Last().Last() : path1.LinearPosition(mSelector.M1); // path1==null when m > poyline length => return last point
        }

        // Get a Linear Position on a polyline
        private static MapPoint LinearPosition(this IEnumerable<MapPoint> points, double m)
        {
            var mSelector = new MSelector(m, m);
            var seg = points.ToSegments().FirstOrDefault(segment => mSelector.Step(segment.Length()));
            return seg == null ? points.Last() : seg.LinearPosition(mSelector.M1);
        }
        #endregion

        #region Length
        // Length of a polyline
        public static double Length(this Polyline polyline)
        {
            return polyline.Paths.Sum(path => path.Length()); // Sum Lengths of Paths
        }

        // Length of one path
        private static double Length(this IEnumerable<MapPoint> points)
        {
            return points.ToSegments().Sum(segment => segment.Length()); // Sum Lengths of Segments
        }
        #endregion

        #region Segment Class
        private class Segment
        {
            public Segment(MapPoint point1, MapPoint point2)
            {
                Point1 = point1;
                Point2 = point2;
            }
            public MapPoint Point1 { get; set; }
            public MapPoint Point2 { get; set; }

            public double Length()
            {
                return Math.Sqrt(Math.Pow(Point2.X - Point1.X, 2) + Math.Pow(Point2.Y - Point1.Y, 2));
            }

            public MapPoint LinearPosition(double m)
            {
                var ratio = m < 0 || Length() == 0 ? 0 : m > Length() ? 1 : m / Length();
                return new MapPoint(Point1.X + ratio * (Point2.X - Point1.X), Point1.Y + ratio * (Point2.Y - Point1.Y));
            }
        }

        private static IEnumerable<Segment> ToSegments(this IEnumerable<MapPoint> points)
        {
            return points.Zip(points.Skip(1), (pt1, pt2) => new Segment(pt1, pt2));
        }

        private static PointCollection ToPointCollection(this IEnumerable<Segment> segments)
        {
            return new PointCollection(segments.Select(s => s.Point1).Concat(Enumerable.Repeat(segments.Last().Point2, 1)));
        }

        #endregion

        #region private class MSelector
        // Helper class to manage the path segments needed to cover a path from M1 to M2
        private class MSelector
        {
            private bool _started;
            private bool _finish;

            public MSelector(double m1, double m2)
            {
                if (m2 < m1)
                    throw new NotSupportedException("m2 must be greater than m1");
                M1 = m1;
                M2 = m2;
            }
            public double M1 { get; private set; }
            public double M2 { get; private set; }
            public bool M2Overflow { get { return !_finish; } }

            public bool Step(double len)
            {
                if (_finish) // we are after M2
                    return false;

                _finish = len >= M2; // containing M2 ==> last segment covering M1-M2
                if (!_finish)
                    M2 -= len;

                _started |= len >= M1; // containing M1 ==> first segement covering M1-M2
                if (!_started)
                    M1 -= len;
                return _started;
            }
        }
        #endregion
    }
}