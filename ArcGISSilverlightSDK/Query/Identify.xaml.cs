using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Tasks;

namespace ArcGISSilverlightSDK
{
    public partial class Identify : UserControl
    {
        public Identify()
        {
            InitializeComponent();
        }

        private void OnMouseClick(object sender, Map.MouseEventArgs e)
        {
            var layer = MyMap.Layers["Water Network"] as ArcGISDynamicMapServiceLayer;
            if (layer == null)
                throw new Exception("Water Network not found");
            if (!layer.Visible)
            {
                MessageBox.Show("Water Network layer is not visible.");
                MyMap.Cursor = Cursors.Arrow;
                return;
            }

            MapPoint clickPoint = e.MapPoint;
            bool takeCareOfScale = CkbScaleVisibility.IsChecked.HasValue && CkbScaleVisibility.IsChecked.Value;

            var identifyParams = new IdentifyParameters
            {
                Geometry = clickPoint,
                MapExtent = MyMap.Extent,
                Width = (int)MyMap.ActualWidth,
                Height = (int)MyMap.ActualHeight,
                LayerOption = takeCareOfScale ? LayerOption.visible : LayerOption.all,
                Tolerance = 10,
                SpatialReference = MyMap.SpatialReference
            };

            // set LayerIds with the layers currently visible at client side (note that setting layerOption to visible is not enough to show only the layer visible at client side)
            if (layer.VisibleLayers != null)
            {
                if (layer.VisibleLayers.Any())
                    identifyParams.LayerIds.AddRange(layer.VisibleLayers);
                else
                {
                    MessageBox.Show("No Water Network sublayers are visible.");
                    MyMap.Cursor = Cursors.Arrow;
                    return;
                }
            }

            var identifyTask = new IdentifyTask(layer.Url);
            identifyTask.ExecuteCompleted += IdentifyTaskOnExecuteCompleted;
            identifyTask.Failed += IdentifyTaskFailed;
            identifyTask.ExecuteAsync(identifyParams);
            MyMap.Cursor = Cursors.Wait;

            var graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            if (graphicsLayer != null)
            {
                graphicsLayer.ClearGraphics();
                var graphic = new Graphic
                {
                    Geometry = clickPoint,
                    Symbol = LayoutRoot.Resources["DefaultPictureSymbol"] as ESRI.ArcGIS.Client.Symbols.Symbol
                };
                graphicsLayer.Graphics.Add(graphic);
            }
        }

        public void ShowFeatures(List<IdentifyResult> results)
        {
            if (results != null && results.Count > 0)
            {
                FeatureSelector.Items.Clear();
                foreach (IdentifyResult result in results)
                {
                    Graphic feature = result.Feature;
                    string title = result.Value + " (" + result.LayerName + ")";
                    var item = new DataItem
                    {
                        Title = title,
                        Data = feature.Attributes,
                        Geometry = feature.Geometry
                    };
                    FeatureSelector.Items.Add(item);
                    CreateGraphic(item);
                }
                FeatureSelector.UpdateLayout();
                FeatureSelector.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Creates a graphic corresponding to a dataitem.
        /// </summary>
        /// <param name="data">The data.</param>
        private void CreateGraphic(DataItem data)
        {
            Geometry geometry = data.Geometry;
            var graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            if (graphicsLayer != null)
            {
                var graphic = new Graphic {Geometry = geometry};
                if (geometry is MapPoint || geometry is MultiPoint)
                    graphic.Symbol = LayoutRoot.Resources["HighlightSymbol"] as ESRI.ArcGIS.Client.Symbols.Symbol;
                else if (geometry is Polyline)
                    graphic.Symbol = LayoutRoot.Resources["HighlightLine"] as ESRI.ArcGIS.Client.Symbols.Symbol;
                if (geometry is Envelope || geometry is Polygon)
                    graphic.Symbol = LayoutRoot.Resources["HighlightFill"] as ESRI.ArcGIS.Client.Symbols.Symbol;

                // Init attributes from data 
                foreach (String key in data.Data.Keys)
                    graphic.Attributes.Add(key, data.Data[key]);
                graphic.Attributes.Add("_title", data.Title); // Add title as attribute so we can use it in mapTipTemplate
                graphicsLayer.Graphics.Add(graphic);
            }
        }

        private void IdentifyTaskOnExecuteCompleted(object sender, IdentifyEventArgs args)
        {
            MyMap.Cursor = Cursors.Stylus;
            FeatureSelector.Items.Clear();
            FeatureSelector.UpdateLayout();

            if (args.IdentifyResults != null && args.IdentifyResults.Count > 0)
            {
                IdentifyResultsPanel.Visibility = Visibility.Visible;
                ShowFeatures(args.IdentifyResults);
            }
            else
            {
                IdentifyResultsPanel.Visibility = Visibility.Collapsed;
            }
        }

        void IdentifyTaskFailed(object sender, TaskFailedEventArgs e)
        {
            MyMap.Cursor = Cursors.Stylus;
            MessageBox.Show("Identify failed. Error: " + e.Error);
        }

        private void GraphicsLayerOnMouseEnter(object sender, GraphicMouseEventArgs e)
        {
            // Synchronize the feature details when hovering the graphic
            var g = e.Graphic;
            if (g.Attributes != null && g.Attributes.ContainsKey("_title"))
            {
                ((GraphicsLayer) sender).ClearSelection();
                g.Select();
                FeatureSelector.SelectedValue = g.Attributes["_title"] as string;
            }
        }

        private void FeatureSelectorOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object item = ((ComboBox) sender).SelectedItem;
            var graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            if (item is DataItem && graphicsLayer != null)
            {
                string title = ((DataItem) item).Title;
                Graphic graphic =
                    graphicsLayer.Graphics.FirstOrDefault(
                        g => g.Attributes != null && g.Attributes.ContainsKey("_title") && g.Attributes["_title"].Equals(title));
                graphicsLayer.ClearSelection();
                if (graphic != null)
                    graphic.Select();
            }
        }

        private void ZoomToFullExtent(object sender, EventArgs args)
        {
            // Zoom to full extent of the layer
            ZoomToFullExtent(MyMap, sender as Layer, 0.5);
        }

        // Zoom To Full Extent of a Layer. If needed:
        //   - wait for map spatial reference initialization
        //   - project the extent
        private static void ZoomToFullExtent(Map map, Layer layer, double expandFactor)
        {
            // ZoomToFullExtent action supposing the map SR is initialized
            Action zoomToFullExtent = () =>
            {
                var extent = layer.FullExtent;

                if (extent != null)
                {
                    extent = extent.Expand(expandFactor);
                    if (map.SpatialReference.Equals(extent.SpatialReference))
                        map.ZoomTo(extent);
                    else
                    {
                        var geometryService = new GeometryService("http://sampleserver1.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer");
                        geometryService.ProjectCompleted += (s, e) => map.ZoomTo(e.Results.First().Geometry);
                        geometryService.ProjectAsync(new[] { new Graphic { Geometry = extent } }, map.SpatialReference);
                    }
                }
            };

            if (map.SpatialReference != null)
                // SR initialized --> we can zoom immediatly
                zoomToFullExtent();
            else
            {
                // SR not initialized, subscribe to Map.PropertyChanged event and wait for the SR
                PropertyChangedEventHandler propertyChangedHandler = null;
                propertyChangedHandler = (s, e) =>
                {
                    if (e.PropertyName == "SpatialReference" && map.SpatialReference != null)
                    {
                        map.PropertyChanged -= propertyChangedHandler;
                        zoomToFullExtent();
                    }
                };
                map.PropertyChanged += propertyChangedHandler;
            }
        }

        #region Class DataItem
        /// <summary>
        /// Item of the identify combo box
        /// </summary>
        public class DataItem
        {
            public string Title { get; set; }
            public IDictionary<string, object> Data { get; set; }
            internal Geometry Geometry;
        }
        #endregion
    }
}

