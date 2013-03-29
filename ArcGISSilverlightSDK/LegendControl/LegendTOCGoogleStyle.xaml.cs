using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Client.Toolkit;
using ESRI.ArcGIS.Client.Toolkit.Primitives;

namespace ArcGISSilverlightSDK
{
    public partial class LegendTOCGoogleStyle : UserControl
    {
        public LegendTOCGoogleStyle()
        {
            InitializeComponent();
        }

        private void LayerItem_OnClick(object sender, RoutedEventArgs e)
        {
            var isChecked = ((CheckBox)sender).IsChecked;
            if (!isChecked.HasValue) return;
            var layerItem = ((CheckBox)sender).DataContext as LayerItemViewModel;

            // set visibilities of the layer and all its descendants
            SetVisibilities(layerItem, isChecked.Value);

            // reset the status of all checkboxes depending on visibilities of descendants
            SetStatus(MyLegend);
        }

        // set visibility of a layer item and all its descendants
        // Note : would be easier to use SetLayerVisibility but that would generate useless requests when changing sublayer visibilities one by one
        // and due a bug, which is fixed in the next version, the latest request may be overwritten by a previous request.
        // So : create a list of visible sublayers and set ArcGISDynamicMapServiceLayer.VisibleLayers
        private static void SetVisibilities(LayerItemViewModel layerItem, bool isVisible)
        {
            if (layerItem.IsMapLayer)
            {
                layerItem.Layer.Visible = isVisible;
                if (layerItem.Layer is ArcGISDynamicMapServiceLayer)
                    ((ArcGISDynamicMapServiceLayer)layerItem.Layer).VisibleLayers = isVisible ? GetSublayerIds(layerItem).ToArray() : new int[0];
                else if (layerItem.Layer is GroupLayer && layerItem.LayerItems != null) 
                    foreach(var item in layerItem.LayerItems)
                        SetVisibilities(item, isVisible);
            }
            else if (layerItem.Layer is ArcGISDynamicMapServiceLayer)
            {
                IEnumerable<int> vis = ((ArcGISDynamicMapServiceLayer)layerItem.Layer).VisibleLayers;
                var sublayerIds = GetSublayerIds(layerItem);
                // Exclude or Add the sublayerids from the current VisibleLayers array
                ((ArcGISDynamicMapServiceLayer) layerItem.Layer).VisibleLayers = (isVisible ? vis.Union(sublayerIds) : vis.Except(sublayerIds)).ToArray();
            }
        }

        private static IList<int> GetSublayerIds(LayerItemViewModel layerItem)
        {
            return layerItem.LayerItems == null ? new List<int> { layerItem.SubLayerID } : layerItem.LayerItems.Select(GetSublayerIds).Aggregate((l1, l2) => l1.Concat(l2).ToList());
        }

        // set recursively the status of the checkboxes (stored in tag)
        private static void SetStatus(Legend legend)
        {
            foreach (var layerItem in legend.LayerItems)
                SetStatus(layerItem);
        }

        private static bool? SetStatus(LayerItemViewModel item)
        {
            bool? result;
            var subitems = item.LayerItems;
            if (subitems != null && subitems.Any())
            {
                // set the status of the children recursively and deduce the status of the parent
                var status = subitems.Select(SetStatus).ToArray();
                if (status.All(s => s == true))
                    result = true; // all subalyers are visible
                else if (status.All(s => s == false))
                    result = false; // all sublayers are invisible
                else
                    result = null; // mixed -> 3rd state
            }
            else
            {
                // leaves : no need for recursivity
                if (item.IsMapLayer)
                    result = item.Layer.Visible;
                else if (item.Layer is ISublayerVisibilitySupport)
                    result = ((ISublayerVisibilitySupport)item.Layer).GetLayerVisibility(item.SubLayerID);
                else
                    result = null;
            }
            item.Tag = result;
            return result;
        }

        private void Legend_Refreshed(object sender, Legend.RefreshedEventArgs e)
        {
            // init status of checkboxes
            SetStatus(sender as Legend);

            // hack to initialize VisibleLayers and get rid of null case
            if (e.LayerItem.Layer is ArcGISDynamicMapServiceLayer)
            {
                var layer = e.LayerItem.Layer as ArcGISDynamicMapServiceLayer;
                layer.SetLayerVisibility(0, !layer.GetLayerVisibility(0));
                layer.SetLayerVisibility(0, !layer.GetLayerVisibility(0));
            }
        }

        // Activate/Deactivate swatches just by setting LegendItemTemplate
        private static DataTemplate _legendItemTemplate; // initial LegendItemTemplate
        private void Swatches_Checked(object sender, RoutedEventArgs e)
        {
            if (MyLegend == null) return;
            MyLegend.LegendItemTemplate = _legendItemTemplate;
        }

        private void Swatches_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MyLegend == null) return;
            _legendItemTemplate = MyLegend.LegendItemTemplate;
            MyLegend.LegendItemTemplate = null;
        }

        #region ZoomToFullExtent (not tied to this sample)
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
        
        #endregion
    }
}
