using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Tasks;

namespace ArcGISSilverlightSDK
{
    public partial class LegendTOC : UserControl
    {
        public LegendTOC()
        {
            InitializeComponent();
        }

        // Activate/Deactivate swatches just by setting LegendItemTemplate
        private static DataTemplate _legendItemTemplate; // initial LegendItemTemplate
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (MyLegend == null) return;
            MyLegend.LegendItemTemplate = _legendItemTemplate;
        }
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MyLegend == null) return;
            _legendItemTemplate = MyLegend.LegendItemTemplate;
            MyLegend.LegendItemTemplate = null;
        }

        private void ZoomToFullExtent(object sender, EventArgs args)
        {
            // Zoom to full extent of the layer
            ZoomToFullExtent(MyMap, sender as Layer);
        }

        // Zoom To Full Extent of a Layer. If needed:
        //   - wait for map spatial reference initialization
        //   - project the extent
        private static void ZoomToFullExtent(Map map, Layer layer)
        {
            // ZoomToFullExtent action supposing the map SR is initialized
            Action zoomToFullExtent = () =>
            {
                var extent = layer.FullExtent;

                if (extent != null)
                {
                    extent = extent.Expand(0.8);
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
