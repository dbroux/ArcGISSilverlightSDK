using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Client.Toolkit;
using ESRI.ArcGIS.Client.Toolkit.Primitives;

namespace ArcGISSilverlightSDK
{
    public partial class LegendReorganize : UserControl
    {
        public LegendReorganize()
        {
            InitializeComponent();
        }

        private LayerItemViewModel _featureLayerItem;
        private void Legend_Refreshed(object sender, Legend.RefreshedEventArgs e)
        {
            var legend = sender as Legend;
            if (legend == null)
                return;
            if (e.LayerItem.Layer is FeatureLayer)
                _featureLayerItem = e.LayerItem; // store feature layer item

            // Insert feature layer item in dynamic layer items
            // The replacement is based on the label --> that supposes the feature layer has the same label that the sublayer in the dynamic layer items
            LayerItemViewModel dynamicLayerItem = legend.LayerItems.FirstOrDefault(l => l.Layer is ArcGISDynamicMapServiceLayer);
            if (dynamicLayerItem != null && dynamicLayerItem.LayerItems != null && _featureLayerItem != null)
            {
                // look for the group layer containing the sublayer to replace
                var groupLayerItem = dynamicLayerItem.LayerItems.FirstOrDefault(layerItem => layerItem.LayerItems.Any(l => l.Label == _featureLayerItem.Label));
                if (groupLayerItem != null)
                {
                    var subLayerItem = groupLayerItem.LayerItems.FirstOrDefault(l => l.Label == _featureLayerItem.Label);
                    if (subLayerItem != _featureLayerItem) // else replacement already done
                    {
                        var pos = groupLayerItem.LayerItems.IndexOf(subLayerItem);
                        groupLayerItem.LayerItems.Remove(subLayerItem); // remove sub layer from legend
                        groupLayerItem.LayerItems.Insert(pos, _featureLayerItem); // replace it at the same position by the feature layer
                        legend.LayerItems.Remove(_featureLayerItem); // remove feature layer from the legend root (else it would be duplicated, but that would work as well)
                    }
                }

                dynamicLayerItem.LayerItems[0].IsExpanded = false; // close the first item not interesting for this demo.
            }
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

        private void FeatureLayer_Initialized(object sender, EventArgs e)
        {
            MyMapTip.GraphicsLayer = sender as GraphicsLayer;
        }

        private void ArcGISDynamicMapServiceLayer_Initialized(object sender, EventArgs e)
        {
            ((ISublayerVisibilitySupport)sender).SetLayerVisibility(23, false);
            ZoomToFullExtent(MyMap, sender as Layer, 0.3);
        }

    }
}
