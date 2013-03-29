using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using System.Linq;

namespace ArcGISSilverlightSDK
{
    public partial class PanWithAnimation : UserControl
    {

        public PanWithAnimation()
        {
            InitializeComponent();
            DataContext = new PanToCommand(MyMap);
        }

        public ICommand PanToCommand { get; set; }

    }

    /// <summary>
    /// Command panning with animation
    /// </summary>
    public class PanToCommand : ICommand
    {
        private Envelope _zoomIn;
        private readonly Map _map;

        // Constructor
        public PanToCommand(Map map)
        {
            _map = map;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable 0067 // never used but needs to be implemented due to interface
        public event EventHandler CanExecuteChanged;
#pragma warning restore 0067

        public void Execute(object parameter)
        {
            if (_map != null && _map.Extent != null && parameter is string)
            {
                var points = ((string)parameter).Split(' ', ',').Select(s => Convert.ToDouble(s, CultureInfo.InvariantCulture)).ToArray();
                var point = new MapPoint(points[0], points[1]);
                PanTo(point);
            }
        }

        private void PanTo(MapPoint point)
        {
            _zoomIn = null;
            if (_map.Extent.Intersects(point.Extent))
                _map.PanTo(point);
            else
            {
                var width = _map.Extent.Width;
                var height = _map.Extent.Height;
                _zoomIn = new Envelope(point.X - width/2, point.Y - height/2, point.X + width/2, point.Y + height/2);

                // Zoom out first
                Envelope zoomOut = _map.Extent.Union(_zoomIn);
                _map.ZoomTo(zoomOut);
                _map.ExtentChanged += map_ExtentChanged;
            }
        }

        void map_ExtentChanged(object sender, ExtentEventArgs e)
        {
            // Zoom In
            var map = sender as Map;
            if (map == null)
                return;
            map.ExtentChanged -= map_ExtentChanged;
            if(_zoomIn != null)
                map.ZoomTo(_zoomIn);
        }
    }
}
