using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Symbols;

namespace ArcGISSilverlightSDK
{
    public partial class CustomClusterer : UserControl
    {
        public CustomClusterer()
        {
            InitializeComponent();
        }

        private Clusterer _clusterer;
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (MyMap == null) return;
            ((FeatureLayer)MyMap.Layers["MyFeatureLayer"]).Clusterer = _clusterer;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MyMap == null) return;
            _clusterer = ((FeatureLayer) MyMap.Layers["MyFeatureLayer"]).Clusterer;
            ((FeatureLayer)MyMap.Layers["MyFeatureLayer"]).Clusterer = null;
        }
    }

    public class CustomFlareClusterer : GraphicsClusterer, ILegendSupport
    {
        public CustomFlareClusterer()
        {
            Radius = 50;
        }

        public ResourceDictionary SymbolsDict { get; set; }

        protected override Graphic OnCreateGraphic(GraphicCollection cluster, MapPoint point, int maxClusterCount)
        {
            if (cluster.Count == 1) return cluster[0];
            Symbol symbol;

            var symbolName = string.Format("Symbol{0}", cluster.Count);
            if (SymbolsDict.Contains(symbolName))
                symbol = SymbolsDict[symbolName] as Symbol;
            else
                symbol = SymbolsDict["LargeSymbol"] as Symbol;

            var size = Math.Max(20, Math.Log10(cluster.Count) * 15);

            var graphic = new Graphic { Symbol = symbol, Geometry = point };
            graphic.Attributes.Add("Count", cluster.Count);
            graphic.Attributes.Add("Size", size);
            graphic.Attributes.Add("Color", InterpolateColor(size -20, 40));
            return graphic;
        }

        private static Brush InterpolateColor(double value, double max)
        {
            value = (int)Math.Round(value * 0xFF / max);
            if (value > 0xFF) value = 0xFF;
            else if (value < 0) value = 0;
            return new SolidColorBrush(Color.FromArgb(0xD0, (byte)value, (byte)(0xFF - value), 0));
        }

        #region ILegendSupport Members

        /// <summary>
        /// Queries for the legend infos of the layer.
        /// </summary>
        /// <remarks>
        /// The returned result is encapsulated in a <see cref="LayerLegendInfo" /> object.
        /// This object contains a collection of legenditems.
        /// </remarks>
        /// <param name="callback">The method to call on completion.</param>
        /// <param name="errorCallback">The method to call in the event of an error.</param>
        public virtual void QueryLegendInfos(Action<LayerLegendInfo> callback, Action<Exception> errorCallback)
        {
            // Create a dummy unique value renderer which supports ILegendSupport
            UniqueValueRenderer renderer = new UniqueValueRenderer();

            if (SymbolsDict != null)
            {
                int i;
                for (i = 2; ; i++ )
                {
                    string symbolName = string.Format("Symbol{0}", i);
                    if (SymbolsDict.Contains(symbolName))
                    {
                        renderer.Infos.Add(new UniqueValueInfo
                        {
                            Label = string.Format("Cluster {0} cities", i),
                            Symbol = SymbolsDict[symbolName] as Symbol
                        });
                    }
                    else
                    {
                        break;
                    }
                }

                renderer.Infos.Add(new UniqueValueInfo
                                    {
                                        Label = string.Format("Cluster >{0} cities", --i),
                                        Symbol = SymbolsDict["LargeSymbol"] as Symbol
                                    });
            }

            renderer.QueryLegendInfos(callback, errorCallback);
        }


        /// <summary>
        /// Occurs when the legend of the layer changed (e.g. when the FlareBackground or the FlareForeground changed)
        /// </summary>
        public event EventHandler<EventArgs> LegendChanged;

        private void OnLegendChanged()
        {
            EventHandler<EventArgs> legendChanged = LegendChanged;
            if (legendChanged != null)
                legendChanged(this, EventArgs.Empty);
        }

        #endregion
    }
}
