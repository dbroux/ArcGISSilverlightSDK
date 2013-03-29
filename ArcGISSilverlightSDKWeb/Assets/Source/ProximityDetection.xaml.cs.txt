using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;

namespace ArcGISSilverlightSDK
{
    public partial class ProximityDetection : UserControl
    {
        readonly GraphicsLayer _graphicsLayer;
        private GraphicsLayer _bufferLayer;
        private GraphicsLayer _bufferLayer2;
        private Task _proximityTask;
        private bool _proximityNeedUpdate;
        private Task _bufferTask;
        private bool _bufferNeedUpdate;
        private bool _useGeodesic;

        public ProximityDetection()
        {
            InitializeComponent();

            _graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            _bufferLayer = MyMap.Layers["BufferResult"] as GraphicsLayer;
            _bufferLayer2 = MyMap.Layers["BufferResult2"] as GraphicsLayer;

            DataContext = this;
            MyMap.Layers.LayersInitialized += Layers_LayersInitialized;
        }

        void Layers_LayersInitialized(object sender, EventArgs args)
        {
            MyMap.Layers.LayersInitialized -= Layers_LayersInitialized;
            _useGeodesic = MyMap.SpatialReference.Equals(new SpatialReference(4326));
            Distance = 200000;
            GeneratePoints(100);
            CalculateProximity();
            CalculateBuffers();
        }

        #region DP Distance
        /// <summary>
        /// The <see cref="Distance" /> dependency property's name.
        /// </summary>
        public const string DistancePropertyName = "Distance";

        /// <summary>
        /// Gets or sets the value of the <see cref="Distance" />
        /// property. This is a dependency property.
        /// </summary>
        public double Distance
        {
            get
            {
                return (double)GetValue(DistanceProperty);
            }
            set
            {
                SetValue(DistanceProperty, value);
            }
        }

        /// <summary>
        /// Identifies the <see cref="Distance" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty DistanceProperty = DependencyProperty.Register(
            DistancePropertyName,
            typeof(double),
            typeof(ProximityDetection),
            new PropertyMetadata(0.0, OnDistanceChanged));

        private static void OnDistanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProximityDetection) d).CalculateProximity();
            ((ProximityDetection)d).CalculateBuffers();
        }
        #endregion

        #region CalculateProximity
        private void CalculateProximity()
        {
            var graphics = _graphicsLayer.Graphics.Where(g => g.Geometry is MapPoint).ToList();
            if (!graphics.Any())
                return;

            if (_proximityTask != null)
                _proximityNeedUpdate = true; // delay the calculation
            else
            {
                _proximityNeedUpdate = false;
                var distance = 2 * Distance;

                // Calculate proximity in background to keep responsive UI
                _proximityTask = Task.Factory.StartNew(() => CalculateProximity(graphics, distance, _useGeodesic))
                    .ContinueWith(task =>
                    {
                        // Go back to UI thread to display the result
                        _proximityTask = null;
                        if (_proximityNeedUpdate)
                            CalculateProximity(); // meanwhile the distance changed

                        var results = task.Result;
                        foreach (var gra in graphics)
                            gra.Attributes["Proximity"] = results.ContainsKey(gra) ? "Y" : "N";
                        foreach (var buffer in _bufferLayer.Graphics)
                        {
                            var associatedGraphic = buffer.Attributes["_AG"] as Graphic;
                            buffer.Attributes["Proximity"] = results.ContainsKey(associatedGraphic) ? "Y" : "N";
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        static private IDictionary<Graphic, bool> CalculateProximity(IEnumerable<Graphic> graphics, double distance, bool useGeodesic)
        {
            var result = new Dictionary<Graphic, bool>(graphics.Count());

            foreach (var gra1 in graphics.Where(gra1 => !result.ContainsKey(gra1)))
            {
                var g1 = gra1;
                foreach (var gra2 in graphics.Where(g => g != g1))
                {
                    if (useGeodesic ? IsCloseGeodesic(gra1, gra2, distance) : IsCloseEuclidian(gra1, gra2, distance))
                    {
                        result.Add(gra1, true);
                        if (!result.ContainsKey(gra2))
                            result.Add(gra2, true);
                        break;
                    }
                }
            }
            return result;
        }

        static private bool IsCloseEuclidian(Graphic gra1, Graphic gra2, double distance)
        {
            var dX = Math.Abs(((MapPoint)gra1.Geometry).X - ((MapPoint)gra2.Geometry).X);
            var dY = Math.Abs(((MapPoint)gra1.Geometry).Y - ((MapPoint)gra2.Geometry).Y);

            return dX <= distance && dY <= distance && dX * dX + dY * dY <= distance * distance;
        }

        // Haversine formula : d=2*asin(sqrt((sin((lat1-lat2)/2))^2 + 
        //  cos(lat1)*cos(lat2)*(sin((lon1-lon2)/2))^2))
        private const double EarthRadius = 6371000;
        private const double ToRadians = Math.PI/180;
        static private bool IsCloseGeodesic(Graphic gra1, Graphic gra2, double distance)
        {
            var lat1 = ((MapPoint)gra1.Geometry).Y * ToRadians;
            var lat2 = ((MapPoint)gra2.Geometry).Y * ToRadians;
            var dLat = lat1 - lat2;
            var dLon = (((MapPoint)gra1.Geometry).X - ((MapPoint)gra2.Geometry).X) * ToRadians;
            var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
            var dist = 2* EarthRadius*Math.Asin(Math.Sqrt(a));
            return dist <= distance;
        }

        #endregion

        #region CalculateBuffers
        private void CalculateBuffers()
        {
            var graphics = _graphicsLayer.Graphics.Where(g => g.Geometry is MapPoint).ToList();
            if (!graphics.Any())
                return;

            if (_bufferTask != null)
                _bufferNeedUpdate = true; // delay the calculation
            else
            {
                _bufferNeedUpdate = false;
                var distance = Distance;

                // Calculate buffers in background to keep responsive UI
                _bufferTask = Task.Factory.StartNew(() => CalculateBuffers(graphics, distance, _useGeodesic))
                    .ContinueWith(task =>
                    {
                        // Go back to UI thread to display the result
                        _bufferTask = null;
                        if (_bufferNeedUpdate)
                            CalculateBuffers(); // meanwhile the distance changed

                        var graphicCollection = new GraphicCollection(task.Result.Zip(graphics, (pol, gra) =>
                        {
                            var buffer = new Graphic { Geometry = pol };
                            foreach (var att in gra.Attributes)
                                buffer.Attributes.Add(att.Key, att.Value);
                            buffer.Attributes.Add("_AG", gra);
                            return buffer;
                        }));

                        // use 2 layers to avoid flicking effect
                        _bufferLayer2.Graphics = graphicCollection;
                        _bufferLayer2.Visible = true;
                        _bufferLayer.Visible = false;
                        var tmp = _bufferLayer;
                        _bufferLayer = _bufferLayer2;
                        _bufferLayer2 = tmp;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private static IList<Polygon> CalculateBuffers(IEnumerable<Graphic> graphics, double distance, bool useGeodesic)
        {
            return graphics.Select(g => useGeodesic ? BufferGeodesic(g, distance) : BufferEuclidian(g, distance)).ToList();
        }

        static Polygon BufferEuclidian(Graphic g, double distance)
        {
            const double dAng = Math.PI / 12;
            var x = ((MapPoint) g.Geometry).X;
            var y = ((MapPoint)g.Geometry).Y;

            var points = new PointCollection();
            for (double ang = 0.0; ang <= 2 * Math.PI; ang += dAng)
            {
                points.Add(new MapPoint(x + distance * Math.Cos(ang), y + distance * Math.Sin(ang)));
            }
            var polygon = new Polygon();
            polygon.Rings.Add(points);
            return polygon;
        }

        // Given a start point, initial bearing, and distance, destination point is given by formula:
        //var lat2 = Math.asin(Math.sin(lat1) * Math.cos(d / R) +
        //      Math.cos(lat1) * Math.sin(d / R) * Math.cos(brng));
        //var lon2 = lon1 + Math.atan2(Math.sin(brng) * Math.sin(d / R) * Math.cos(lat1),
        //                     Math.cos(d / R) - Math.sin(lat1) * Math.sin(lat2));
        static Polygon BufferGeodesic(Graphic g, double distance)
        {
            const double dAng = Math.PI / 12;
            var lat1 = ((MapPoint)g.Geometry).Y * ToRadians;
            var lon1 = ((MapPoint)g.Geometry).X * ToRadians;

            // Precalculate some constant values in order not to do it in the loop
            var sind = Math.Sin(distance/EarthRadius);
            var cosd = Math.Cos(distance / EarthRadius);
            var c1 = Math.Sin(lat1) * cosd;
            var c2 = Math.Cos(lat1)*sind;
            var sinl1 = Math.Sin(lat1);

            var points = new PointCollection();
            for (double ang = 0.0; ang <= 2 * Math.PI; ang += dAng)
            {
                var lat2 = Math.Asin(c1 + c2 * Math.Cos(ang));
                var lon2 = lon1 + Math.Atan2(Math.Sin(ang) * c2, cosd - sinl1 * Math.Sin(lat2));
                
                points.Add(new MapPoint(lon2 / ToRadians, lat2 / ToRadians));
            }
            var polygon = new Polygon();
            polygon.Rings.Add(points);
            return polygon;
        } 

        #endregion

        #region GeneratePoints
        private static int _itemId;

        // generate nb random points in the current extent
        private void GeneratePoints(int nb)
        {
            var rand = new Random();
            for (var i = 0; i < nb; i++)
            {
                var x = MyMap.Extent.XMin + rand.NextDouble() * (MyMap.Extent.Width);
                var y = MyMap.Extent.YMin + rand.NextDouble() * (MyMap.Extent.Height);
                if (_useGeodesic)
                {
                    if (y > 90)
                        y = 180 - y;
                    if (y < -90)
                        y = y - 180;
                }
                var graphic = new Graphic { Geometry = new MapPoint(x, y) };
                graphic.Attributes["ID"] = string.Format("Item#{0}", ++_itemId);
                _graphicsLayer.Graphics.Add(graphic);
            }
        }

        private void GenerateRandomPoints(object sender, RoutedEventArgs e)
        {
            GeneratePoints(100);
            CalculateProximity();
            CalculateBuffers();
        }

        private void ClearPoints(object sender, RoutedEventArgs e)
        {
            _graphicsLayer.ClearGraphics();
            _bufferLayer.ClearGraphics();
            _bufferLayer2.ClearGraphics();
        }
        
        #endregion
    }
}
