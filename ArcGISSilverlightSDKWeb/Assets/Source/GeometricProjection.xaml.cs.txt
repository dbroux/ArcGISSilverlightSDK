using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ArcGISSilverlightSDK.GeometricProjectionExtensions;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Symbols;

namespace ArcGISSilverlightSDK
{
    public partial class GeometricProjection : UserControl
    {
        private readonly Draw _drawObject;
        private Symbol _activeSymbol;
        readonly GraphicsLayer _graphicsLayer;
        readonly GraphicsLayer _resultLayer;
        private bool _drawing;

        public GeometricProjection()
        {
            InitializeComponent();

            _graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            _resultLayer = MyMap.Layers["projectionResult"] as GraphicsLayer;

            _drawObject = new Draw(MyMap)
            {
                LineSymbol = LayoutRoot.Resources["DrawLineSymbol"] as LineSymbol,
                FillSymbol = LayoutRoot.Resources["DrawFillSymbol"] as FillSymbol
            };
            _drawObject.DrawComplete += (s, e) =>
            {
                var graphic = new Graphic
                {
                    Geometry = e.Geometry,
                    Symbol = _activeSymbol,
                };
                _graphicsLayer.Graphics.Add(graphic);
                _drawing = false;
                CalculateProjection(_point);
            };
            _drawObject.DrawBegin += (s, e) => _drawing = true;
        }

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
                case "DrawPoint":
                    _drawObject.DrawMode = DrawMode.Point;
                    _activeSymbol = LayoutRoot.Resources["DefaultMarkerSymbol"] as Symbol;
                    break;
                case "DrawPolyline":
                    _drawObject.DrawMode = DrawMode.Polyline;
                    _activeSymbol = LayoutRoot.Resources["DefaultLineSymbol"] as Symbol;
                    break;
                case "DrawlineSegment":
                    _drawObject.DrawMode = DrawMode.LineSegment;
                    _activeSymbol = LayoutRoot.Resources["DefaultLineSymbol"] as Symbol;
                    break;
                case "DrawPolygon":
                    _drawObject.DrawMode = DrawMode.Polygon;
                    _activeSymbol = LayoutRoot.Resources["DefaultFillSymbol"] as Symbol;
                    break;
                case "DrawRectangle":
                    _drawObject.DrawMode = DrawMode.Rectangle;
                    _activeSymbol = LayoutRoot.Resources["DefaultFillSymbol"] as Symbol;
                    break;
                case "DrawFreehand":
                    _drawObject.DrawMode = DrawMode.Freehand;
                    _activeSymbol = LayoutRoot.Resources["DefaultLineSymbol"] as Symbol;
                    break;
                case "DrawArrow":
                    _drawObject.DrawMode = DrawMode.Arrow;
                    _activeSymbol = LayoutRoot.Resources["DefaultFillSymbol"] as Symbol;
                    break;
                case "DrawTriangle":
                    _drawObject.DrawMode = DrawMode.Triangle;
                    _activeSymbol = LayoutRoot.Resources["DefaultFillSymbol"] as Symbol;
                    break;
                case "DrawCircle":
                    _drawObject.DrawMode = DrawMode.Circle;
                    _activeSymbol = LayoutRoot.Resources["DefaultFillSymbol"] as Symbol;
                    break;
                case "DrawEllipse":
                    _drawObject.DrawMode = DrawMode.Ellipse;
                    _activeSymbol = LayoutRoot.Resources["DefaultFillSymbol"] as Symbol;
                    break;
                default:
                    _drawObject.DrawMode = DrawMode.None;
                    _graphicsLayer.ClearGraphics();
                    _resultLayer.ClearGraphics();
                    _prj = null;
                    break;
            }
            _drawObject.IsEnabled = (_drawObject.DrawMode != DrawMode.None);
        }

        private Task _task;
        private MapPoint _prj;
        private MapPoint _point;
        private bool _needUpdate;

        private void MyMap_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var point = MyMap.ScreenToMap(e.GetPosition(MyMap));
            if (point == null)
                return;
            _point = point;
            DisplayProjection(_point, _prj);
            CalculateProjection(point);
        }

        private void CalculateProjection(MapPoint point)
        {
            var graphicslayer = MyMap.Layers["FeatureLayer"] as GraphicsLayer;
            if (graphicslayer == null)
                return;
            var geometries = _graphicsLayer.Graphics.Concat(graphicslayer.Graphics).Select(g => g.Geometry).ToArray();
            if (!geometries.Any() || point == null)
                return;

            if (_task != null || _drawing) // don't calculate a new projection if the previous one is still running or while drawing is active
                _needUpdate = true; // delay the calculation
            else
            {
                _needUpdate = false;

                // Calculate projection in background to keep responsive UI
                _task = Task.Factory.StartNew(() => geometries.Projection(point))
                    .ContinueWith(task =>
                    {
                        // Go back to UI thread to display the result
                        _prj = task.Result;
                        DisplayProjection(_point, _prj);
                        _task = null;
                        if (_needUpdate)
                            CalculateProjection(_point); // meanwhile the mouse moved
                    }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        /// <summary>
        /// Displays the projected point as a marker and a line between the current point and the projected point.
        /// </summary>
        private void DisplayProjection(MapPoint point, MapPoint prj)
        {
            if (prj == null)
                return;

            var polyline = new Polyline();
            polyline.Paths.Add(new PointCollection { prj, point });

            if (_resultLayer.Graphics.Any())
            {
                // Reuse the existing graphics (less flicking effect)
                _resultLayer.Graphics[0].Geometry = prj;
                _resultLayer.Graphics[1].Geometry = polyline;
            }
            else
            {
                // Create new graphics
                _resultLayer.Graphics.Add(new Graphic
                {
                    Symbol = LayoutRoot.Resources["ProjectionMarkerSymbol"] as Symbol,
                    Geometry = prj
                });
                _resultLayer.Graphics.Add(new Graphic
                {
                    Symbol = LayoutRoot.Resources["ProjectionLineSymbol"] as Symbol,
                    Geometry = polyline
                });
            }
        }
    }
}

namespace ArcGISSilverlightSDK.GeometricProjectionExtensions
{
    /// <summary> 
    /// Extension class to calculate the projection of a point on the closest geometry
    /// </summary>
    public static class ProjectionExtension
    {
        /// <summary>
        /// Returns the projection of a point on the closest geometry
        /// </summary>
        public static MapPoint Projection(this IEnumerable<Geometry> geometries, MapPoint point)
        {
            return geometries.Where(g => g != null).Select(g => g.Projection(point)).SelectMin(p => Distance2(p, point));
        }

        /// <summary>
        /// Returns the projection of a point on a geometry
        /// </summary>
        private static MapPoint Projection(this Geometry geometry, MapPoint point)
        {
            if (geometry is MapPoint)
                return (MapPoint)geometry;
            if (geometry is Polyline)
                return ((Polyline)geometry).Projection(point);
            if (geometry is Polygon)
                return ((Polygon)geometry).Projection(point);
            if (geometry is Envelope)
                return ((Envelope)geometry).Projection(point);
            if (geometry is MultiPoint)
                return ((MultiPoint)geometry).Projection(point);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the projection of a point on a multi point (i.e the closest point).
        /// </summary>
        private static MapPoint Projection(this MultiPoint multiPoint, MapPoint point)
        {
            return multiPoint.Points.SelectMin(p => Distance2(p, point));
        }

        /// <summary>
        /// Returns the projection of a point on a polyline.
        /// </summary>
        private static MapPoint Projection(this Polyline polyline, MapPoint point)
        {
            return polyline.Paths.Select(points => points.Projection(point)).SelectMin(p => Distance2(p, point));
        }

        /// <summary>
        /// Returns the projection of a point on a polygon.
        /// </summary>
        private static MapPoint Projection(this Polygon polygon, MapPoint point)
        {
            return polygon.Rings.Select(points => points.Projection(point)).SelectMin(p => Distance2(p, point));
        }

        /// <summary>
        /// Returns the projection of a point on an envelope.
        /// </summary>
        private static MapPoint Projection(this Envelope envelope, MapPoint point)
        {
            var topLeft = new MapPoint(envelope.XMin, envelope.YMax);
            var topRight = new MapPoint(envelope.XMax, envelope.YMax);
            var bottomLeft = new MapPoint(envelope.XMin, envelope.YMin);
            var bottomRight = new MapPoint(envelope.XMax, envelope.YMin);
            var points = new[] { topLeft, topRight, bottomRight, bottomLeft, topLeft };
            return points.Projection(point);
        }

        /// <summary>
        /// Returns the projection of a point on a collection of segment.
        /// </summary>
        private static MapPoint Projection(this IEnumerable<MapPoint> points, MapPoint point)
        {
            return points.Zip(points.Skip(1), (pt1, pt2) => new Tuple<MapPoint, MapPoint>(pt1, pt2))
                .Select(seg => ProjectionOnSegment(seg, point))
                .SelectMin(p => Distance2(p, point)) ?? points.FirstOrDefault(); // ?? for the case there is only one point in the enum
        }

        /// <summary>
        /// Returns the projection of a point on a segment.
        /// </summary>
        private static MapPoint ProjectionOnSegment(Tuple<MapPoint, MapPoint> segment, MapPoint point)
        {
            var point1 = segment.Item1;
            var point2 = segment.Item2;
            var dist2 = Distance2(point1, point2);
            if (dist2 == 0)
                return point1;
            double m = (point.X - point1.X) * (point2.X - point1.X) + (point.Y - point1.Y) * (point2.Y - point1.Y);
            m /= Distance2(point1, point2);

            if (m <= 0)
                // Point1 is the closest
                return point1;
            if (m >= 1)
                // Point2 is the closest
                return point2;
            return new MapPoint(point1.X + m * (point2.X - point1.X), point1.Y + m * (point2.Y - point1.Y));
        }

        /// <summary>
        /// Square of the distance between 2 points 
        /// </summary>
        private static double Distance2(MapPoint point1, MapPoint point2)
        {
            var dx = point2.X - point1.X;
            var dy = point2.Y - point1.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Returns the item with a minimun value.
        /// </summary>
        private static T SelectMin<T>(this IEnumerable<T> items, Func<T, double> selector)
        {
            return items.Any() ? items.Aggregate((t1, t2) => selector(t1) > selector(t2) ? t2 : t1) : default(T);
        }
    }
}