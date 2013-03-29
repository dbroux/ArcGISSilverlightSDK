using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Media;
using ArcGISSilverlightSDK.PieChart;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Tasks;

namespace ArcGISSilverlightSDK
{
    public partial class PieChartRendering : UserControl
    {

        public PieChartRendering()
        {
            InitializeComponent();
        }

        private void FeatureLayer_UpdateCompleted(object sender, EventArgs e)
        {
            // Populate the graphics layer with centroids points
            var featureLayer = sender as FeatureLayer;
            var graphicsLayer = MyMap.Layers.OfType<GraphicsLayer>().FirstOrDefault(l => !(l is FeatureLayer));
            new CentroidsHelper().Create(featureLayer, graphicsLayer);
        }
    }

}

namespace ArcGISSilverlightSDK.PieChart
{
    /// <summary>
    /// The ChartMarkerSymbol Class: inherits from ESRI MarkerSymbol class
    /// </summary>
    public class ChartMarkerSymbol : MarkerSymbol
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ChartMarkerSymbol()
        {
            Fields = new FieldCollection();
        }


        /// <summary>
        /// Dependency property for the collection of fields used for the symbology
        /// </summary>
        public FieldCollection Fields
        {
            get { return (FieldCollection)GetValue(FieldsProperty); }
            set { SetValue(FieldsProperty, value); }
        }
        public static readonly DependencyProperty FieldsProperty =
            DependencyProperty.Register("Fields", typeof(FieldCollection), typeof(ChartMarkerSymbol), new PropertyMetadata(new FieldCollection()));
    }

    /// <summary>
    /// The FieldCollection class inheriting from ObservableCollection of the Field class
    /// </summary>
    public class FieldCollection : ObservableCollection<Field> { }


    /// <summary>
    /// The field class
    /// </summary>
    public class Field
    {
        //Property for field names
        public string FieldName { get; set; }
        //Property for display/alias name for each field
        public string DisplayName { get; set; }
        //Property for fill brush of each field in the pie chart symbology
        public Brush Fill { get; set; }
    }

    /// <summary>
    /// A surrogate binding to take care of converting fill brush and the display name of 
    /// each field used in the pie chart symbology
    /// </summary>
    public static class SurrogateBind
    {
        //Default solid color brush for the case no brush set by the user
        private static readonly SolidColorBrush[] Brushes = 
        {   
            new SolidColorBrush(Color.FromArgb(0xFF, 0xB9, 0xD6, 0xF7)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xFB, 0xB7, 0xB5)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xB8, 0xC0, 0xAC)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xFD, 0xE7, 0x9C)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xA9, 0xA3, 0xBD)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xB1, 0xA1, 0xB1)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0xC2, 0xB3)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xB5, 0xB5, 0xB5)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x98, 0xC1, 0xDC)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xC1, 0xC0, 0xAE)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xBD, 0xC0)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x2F, 0x8C, 0xE2)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xDC, 0xDC)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF4, 0xF4)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF4, 0xF4)),

            new SolidColorBrush(Color.FromArgb(0xFF, 0x28, 0x4B, 0x70)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x70, 0x28, 0x28)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x5F, 0x71, 0x43)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xF6, 0xBC, 0x0C)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0x2C, 0x6C)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x50, 0x22, 0x4F)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x1D, 0x75, 0x54)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0x4C, 0x4C)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x02, 0x71, 0xAE)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x70, 0x6E, 0x41)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x44, 0x6A, 0x73)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x0C, 0x3E, 0x69)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0x75, 0x75, 0x75)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xB7, 0xB7, 0xB7)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xA3, 0xA3, 0xA3))
        };

        /// <summary>
        /// Attached property for the collection of fields:
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static FieldCollection GetFieldsCollection(DependencyObject obj)
        {
            return (FieldCollection)obj.GetValue(FieldsCollectionProperty);
        }
        public static void SetFieldsCollection(DependencyObject obj, FieldCollection value)
        {
            obj.SetValue(FieldsCollectionProperty, value);
        }
        public static readonly DependencyProperty FieldsCollectionProperty =
            DependencyProperty.RegisterAttached("FieldsCollection", typeof(FieldCollection), typeof(SurrogateBind), new PropertyMetadata(OnFieldsChanged));


        /// <summary>
        /// Method to create radial gradient brushes for each pie data point (default colors)
        /// </summary>
        /// <param name="gradientStop1"></param>
        /// <param name="gradientStop2"></param>
        /// <returns></returns>
        private static RadialGradientBrush GetRadialGradientBrush(SolidColorBrush gradientStop1, SolidColorBrush gradientStop2)
        {
            const int gradStopOffset = 1;

            var transformGroup = new TransformGroup();

            var scaleTransform = new ScaleTransform
            {
                CenterX = 0.5,
                CenterY = 0.5,
                ScaleX = 2.09,
                ScaleY = 1.819
            };
            transformGroup.Children.Add(scaleTransform);

            var translateTransform = new TranslateTransform { X = -0.425, Y = -0.486 };
            transformGroup.Children.Add(translateTransform);

            var gradStopCollection = new GradientStopCollection();
            var gradStop = new GradientStop { Color = gradientStop1.Color };
            gradStopCollection.Add(gradStop);
            gradStop = new GradientStop { Color = gradientStop2.Color, Offset = gradStopOffset };
            gradStopCollection.Add(gradStop);

            var radGradBrush = new RadialGradientBrush(gradStopCollection) { RelativeTransform = transformGroup };

            return radGradBrush;
        }

        /// <summary>
        /// The property changed call back method for the FieldsCollection attached property
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnFieldsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var chart = d as Chart;
            if (chart != null)
            {
                if (chart.Series.Count == 1)
                {
                    var pieSeries = chart.Series[0] as PieSeries;

                    //Collection of fields passed from the value of the object of type DependencyPropertyChangedEventArgs:
                    var fields = e.NewValue as FieldCollection;
                    if (pieSeries != null && fields != null)
                    {
                        //Getting the chart DataBinding:
                        var chartDataBinding = chart.DataContext as DataBinding;

                        //Getting the Attributes value of the DataBinding in chart:
                        IDictionary<string, object> attributes = chartDataBinding == null ? null : chartDataBinding.Attributes;

                        //The collection to store converted DataContext of the chart elemenet:
                        var pieSeriesItemsSource = new ObservableCollection<KeyValueBrushTriplet>();

                        int idx = 0;

                        foreach (Field field in fields)
                        {
                            if (attributes != null && !attributes.ContainsKey(field.FieldName)) continue;

                            //Converting chart DataContext to KeyValueBrushTriplet of DisplayName, the value in the chart DataContext and the expected color
                            string displayName = string.IsNullOrEmpty(field.DisplayName) ? field.FieldName : field.DisplayName;
                            Brush fill = field.Fill ?? GetRadialGradientBrush(Brushes[idx % 15], Brushes[idx % 15 + 15]);
                            idx++;
                            var keyValueBrushTriplet = new KeyValueBrushTriplet
                                                        {
                                                            Key = displayName,
                                                            Value = attributes == null ? 10 : attributes[field.FieldName], // if attributes == null, set a dummy value for the legend
                                                            Fill = fill
                                                        };
                            pieSeriesItemsSource.Add(keyValueBrushTriplet);
                        }

                        //Setting the ItemsSource of the PieSeries in the chart:
                        pieSeries.ItemsSource = pieSeriesItemsSource;
                    }
                }
            }
        }
    }

    public class KeyValueBrushTriplet
    {
        public string Key { get; set; }
        public object Value { get; set; }
        public Brush Fill { get; set; }
    }

    /// <summary>
    /// Helper class to create centroid points from polygons.
    /// If the center of the polygon is inside the polygon we use it, else we use a geometry service
    /// </summary>
    internal class CentroidsHelper
    {
        //An index used for graphic elements added in the graphics layer:
        int _idx;
        private GraphicsLayer _outLayer;

        public void Create(FeatureLayer inLayer, GraphicsLayer outLayer)
        {
            if (inLayer == null || !inLayer.Graphics.Any())
                return;
            _outLayer = outLayer;
            CreateCentroidGraphics(inLayer.Graphics);
        }

        /// <summary>
        /// Finds centroids of state MBRs and uses pie charts as symbologies
        /// New centroids will be calculated after determining the calculated 
        /// centroid was not inside of the polygons
        /// </summary>
        /// <param name="features">Enumeration of polygons </param>
        void CreateCentroidGraphics(IEnumerable<Graphic> features)
        {
            _outLayer.Graphics.Clear();

            var graphicsForGeometryService = new List<Graphic>();

            if (features != null)
            {
                foreach (Graphic feature in features)
                {
                    var polygon = feature.Geometry as ESRI.ArcGIS.Client.Geometry.Polygon;

                    var graphic = new Graphic();
                    //Assigning attributes from the feature to the graphic:
                    foreach (var kvp in feature.Attributes)
                        graphic.Attributes.Add(kvp);

                    //Getting the center point of the polygon MBR:
                    ESRI.ArcGIS.Client.Geometry.MapPoint featureCentroid = feature.Geometry.Extent.GetCenter();

                    bool pointInPolygon = IsPointInsidePolygon(polygon, featureCentroid);
                    if (pointInPolygon)
                    {
                        graphic.Geometry = featureCentroid;
                        _outLayer.Graphics.Add(graphic);
                    }
                    else
                    {
                        //Using the geometry service to find a new centroid in the case the
                        //calculated centroid is not inside of the polygon:
                        graphic.Geometry = polygon;
                        graphicsForGeometryService.Add(graphic);
                    }
                }
            }

            if (graphicsForGeometryService.Count > 0)
            {
                // Need geometry service to calculate better centroid
                // First simplify the geometry else in some case LabelPoints is not working
                var geometryService = new GeometryService("http://sampleserver1.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer");
                geometryService.SimplifyCompleted += GeometryService_SimplifyCompleted;
                geometryService.Failed += GeometryService_Failed;
                geometryService.SimplifyAsync(graphicsForGeometryService);

                _idx = _outLayer.Graphics.Count;
                // Add the graphics that need geometry service at the end of the graphics layer in order not to loose attributes
                foreach (var graphic in graphicsForGeometryService)
                {
                    graphic.Geometry = graphic.Geometry.Extent.GetCenter(); // default geometry not that bad if geometry service not working
                    _outLayer.Graphics.Add(graphic);
                }
            }
        }

        /// <summary>
        /// Delegate method called when the simplified polygons are available
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void GeometryService_SimplifyCompleted(object sender, GraphicsEventArgs args)
        {
            var geometryService = new GeometryService("http://sampleserver1.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer");
            geometryService.LabelPointsCompleted += GeometryService_LabelPointsCompleted;
            geometryService.Failed += GeometryService_Failed;
            geometryService.LabelPointsAsync(args.Results);
        }

        /// <summary>
        /// Delegate method for updating the geometry of the pie chart marker symbols in newly calculated centroid of the polygon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void GeometryService_LabelPointsCompleted(object sender, GraphicsEventArgs args)
        {
            foreach (Graphic graphic in args.Results)
                _outLayer.Graphics[_idx++].Geometry = graphic.Geometry;
        }

        /// <summary>
        /// Delegate method to inform user about the geometry service failure
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void GeometryService_Failed(object sender, TaskFailedEventArgs e)
        {
            MessageBox.Show("Geometry Service error: " + e.Error);
        }

        /// <summary>
        /// Utility function to determine whether a map point is inside of a given polygon
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="mapPoint"></param>
        /// <returns></returns>
        private static bool IsPointInsidePolygon(ESRI.ArcGIS.Client.Geometry.Polygon polygon, ESRI.ArcGIS.Client.Geometry.MapPoint mapPoint)
        {
            foreach (ESRI.ArcGIS.Client.Geometry.PointCollection points in polygon.Rings)
            {
                int i;
                int j = points.Count - 1;
                bool inPoly = false;

                for (i = 0; i < points.Count; i++)
                {
                    if (points[i].X < mapPoint.X && points[j].X >= mapPoint.X ||
                        points[j].X < mapPoint.X && points[i].X >= mapPoint.X)
                    {
                        if (points[i].Y + (mapPoint.X - points[i].X) / (points[j].X - points[i].X) * (points[j].Y - points[i].Y) < mapPoint.Y)
                        {
                            inPoly = !inPoly;
                        }
                    }
                    j = i;
                }

                if (inPoly)
                    return true;
            }

            return false;
        }

    }

}
