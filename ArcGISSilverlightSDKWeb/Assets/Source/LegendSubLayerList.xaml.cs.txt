using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Client.Toolkit.Primitives;

namespace ArcGISSilverlightSDK
{
    public partial class LegendSubLayerList : UserControl
    {
        public LegendSubLayerList()
        {
            InitializeComponent();
        }

        private void Legend_Refreshed(object sender, ESRI.ArcGIS.Client.Toolkit.Legend.RefreshedEventArgs e)
        {
            // for each layer/sublayer item, report the swatch of the first legend item to the layer item
            foreach(var layerItem in GetAllLayerItems(e.LayerItem).Where(item => item.LegendItems != null && item.LegendItems.Any()))
                layerItem.ImageSource = layerItem.LegendItems.First().ImageSource;
        }

        private static IEnumerable<LayerItemViewModel> GetAllLayerItems(LayerItemViewModel layerItem)
        {
            IEnumerable<LayerItemViewModel> result = new[] {layerItem}; // the layerItem itself
            // Concat recursively with sublayers items
            if (layerItem.LayerItems != null)
                result = result.Concat(layerItem.LayerItems.SelectMany(GetAllLayerItems));
            return result;
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
    }
}
