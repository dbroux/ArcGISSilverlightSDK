using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Projection;
using ESRI.ArcGIS.Client.Symbols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PointCollection = ESRI.ArcGIS.Client.Geometry.PointCollection;

namespace ArcGISSilverlightSDK // todo ok to keep it as sample?
{
	public partial class Measure : UserControl
	{
		public Measure()
		{
			InitializeComponent();
		}

		private void ToggleSR(object sender, RoutedEventArgs e)
		{
			var isChecked = ((CheckBox) sender).IsChecked;
			var wgs84 = isChecked.HasValue && isChecked.Value;
			LayerCollection layers = MyMap.Layers;
//			var layer = MyMap.Layers.OfType<ArcGISTiledMapServiceLayer>().FirstOrDefault();
//			var layers = MyMap.Layers.ToList();
			//layers.Remove(layer);
			//MyMap.Layers.Clear();
			var extent = wgs84
							? new WebMercator().ToGeographic(MyMap.Extent) as Envelope
							: new WebMercator().FromGeographic(MyMap.Extent) as Envelope;
			MyMap.Layers = null;
			MyMap.Extent = null;
			string url = wgs84
							? "http://services.arcgisonline.com/ArcGIS/rest/services/ESRI_StreetMap_World_2D/MapServer"
							: "http://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer";
			layers[0] = new ArcGISTiledMapServiceLayer {Url = url};

			if (double.IsNegativeInfinity(extent.YMin))
				extent.YMin = -SpatialReference.WorldWidth(extent.SpatialReference)/2.0;
			if (double.IsNegativeInfinity(extent.YMax))
				extent.YMax = -SpatialReference.WorldWidth(extent.SpatialReference) / 2.0;
			if (double.IsPositiveInfinity(extent.YMin))
				extent.YMin = SpatialReference.WorldWidth(extent.SpatialReference) / 2.0;
			if (double.IsPositiveInfinity(extent.YMax))
				extent.YMax = SpatialReference.WorldWidth(extent.SpatialReference) / 2.0;
			MyMap.Layers = layers;
			MyMap.Extent = extent;
		}
	}

	/// <summary>
	/// Area units used by the MeasureAction
	/// </summary>
	public enum AreaUnit
	{
		/// <summary>Undefined/Unknown</summary>
		Undefined = 0,
		/// <summary>Square miles</summary>
		SquareMiles = 1,
		/// <summary>Acres</summary>
		Acres = 2,
		/// <summary>Square Kilometers</summary>
		SquareKilometers = 3,
		/// <summary>Square Feet</summary>
		SquareFeet = 4,
		/// <summary>Square Meters</summary>
		SquareMeters = 5,
		/// <summary>Hectares</summary>
		Hectares = 6
	}

	/// <summary>
	/// Distance units used by the MeasureAction
	/// </summary>
	public enum DistanceUnit
	{
		/// <summary>Undefined/Unknown</summary>
		Undefined = 0,
		/// <summary>Decimal degrees</summary>
		DecimalDegrees = 1,
		/// <summary>Miles</summary>
		Miles = 2,
		/// <summary>Kilometers</summary>
		Kilometers = 3,
		/// <summary>Feet</summary>
		Feet = 4,
		/// <summary>Meters</summary>
		Meters = 5,
		/// <summary>Yards</summary>
		Yards = 6,
		/// <summary>Nautical miles</summary>
		NauticalMiles = 7
	}

	internal class MeasureImpl
	{
		private bool _isActivated;
		int _lineCount;
		double _totalLength;
		double _segmentLength;
		double _tempTotalLength;
		MapPoint _originPoint;
		MapPoint _endPoint;
		PointCollection _points;
		Point _lastClick;
		readonly SimpleMarkerSymbol _markerSymbol;
		private bool _isMeasuring;
		//List<double> _lengths;
		private const double WebMercatorXMax = 20037508.3427892;
		//private readonly MeasureHelper _measureHelper = new MeasureHelper();
		private Feedback _feedback;

		internal MeasureImpl()
		{
			//Set up defaults
			FormatString = "{0} {1}";
			MapUnits = DistanceUnit.Undefined;
			NumberDecimals = 2;
			DistanceUnits = DistanceUnit.Undefined;
			AreaUnits = AreaUnit.Undefined;
			Type = MeasureType.Distance;
			GraphicsLayer = new GraphicsLayer();

			_markerSymbol = new SimpleMarkerSymbol
				                {
				Color = new SolidColorBrush(Color.FromArgb(0x66, 255, 0, 0)),
				Size = 5,
				Style = SimpleMarkerSymbol.SimpleMarkerStyle.Circle
			};
			//_lengths = new List<double>();
			_points = new PointCollection();
		}


		#region Properties
		public string FormatString { get; set; }
		public DistanceUnit MapUnits { get; set; }
		public double NumberDecimals { get; set; }
		public Map Map { get; set; }
		private GraphicsLayer GraphicsLayer { get; set; }
		public MeasureType Type { get; set; }
		public double TotalLength { get; set; }
		public double TotalArea { get; set; }
		public AreaUnit AreaUnits { get; set; }
		public DistanceUnit DistanceUnits { get; set; }
		public FillSymbol FillSymbol { get; set; }
		public LineSymbol LineSymbol { get; set; }
		public bool ShowTotals { get; set; }
		public bool IsActivated
		{
			get { return _isActivated; }
			set
			{
				if (_isActivated != value)
				{
					_isActivated = value;
					if (value)
					{
						Map.MouseMove += map_MouseMove;
						Map.MouseLeftButtonDown += map_MouseLeftButtonDown;

						//Map.Layers.Add(GraphicsLayer);
						if (!Map.Layers.Contains(GraphicsLayer))
							Map.Layers.Add(GraphicsLayer);
						Map.Cursor = Cursors.Stylus;
					}
					else
					{
						Map.Cursor = Cursors.Arrow;
						Map.MouseMove -= map_MouseMove;
						Map.MouseLeftButtonDown -= map_MouseLeftButtonDown;
						//Map.Layers.Remove(GraphicsLayer);
						ResetValues();
					}
				}
			}
		}

		#endregion

		public void ResetValues()
		{
			_isMeasuring = false;
			//_originPoint = null;
			_endPoint = null;
			_lineCount = 0;
			_points = new PointCollection();
			//_lengths = new List<double>();
			_totalLength = 0;
			_tempTotalLength = 0;
			_segmentLength = 0;
		}

		public event EventHandler MeasureCompleted;

		private void OnMesureCompleted()
		{
			if (MeasureCompleted != null)
				MeasureCompleted(this, EventArgs.Empty);
		}

		private void map_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (GraphicsLayer.Graphics == null) return; // ??
			e.Handled = true;
			Point pt = e.GetPosition(null);
			if (Math.Abs(pt.X - _lastClick.X) < 2 && Math.Abs(pt.Y - _lastClick.Y) < 2)
			{
				//var result = GraphicsLayer.Graphics[0].Geometry;
				//if (result == null) return;
				//if (Type == MeasureType.Area)
				//{
				//    Polygon poly1 = result as Polygon;
				//    MapPoint firstpoint = poly1.Rings[0][0];
				//    poly1.Rings[0].Add(new MapPoint(firstpoint.X, firstpoint.Y));
				//}
				//GraphicsLayer.Graphics.Clear();
				_feedback.Close();
				_feedback = null;
				IsActivated = false;
				OnMesureCompleted();
			}
			else
			{
				if (_feedback == null)
				{
					_feedback = new Feedback(Type);
					_feedback.GraphicsLayer = GraphicsLayer; // useful?
					_feedback.MapUnits = MapUnits;
					_feedback.FillSymbol = FillSymbol;
					_feedback.LineSymbol = LineSymbol;
				}
				//if (_points.Count == 0)
				//{
				//    GraphicsLayer.Graphics.Clear();
				//    if (Type == MeasureType.Area)
				//    {
				//        Graphic areaGraphic = new Graphic()
				//        {
				//            Symbol = FillSymbol
				//        };

				//        GraphicsLayer.Graphics.Add(areaGraphic);
				//        if (ShowTotals)
				//        {
				//            Graphic areaTotalGraphic = new Graphic()
				//            {
				//                Symbol = new RotatingTextSymbol()
				//                {
				//                    OffsetX = -20,
				//                    OffsetY = -15,
				//                    HorizontalAlignment = HorizontalAlignment.Left
				//                }
				//            };
				//            GraphicsLayer.Graphics.Add(areaTotalGraphic);
				//            areaTotalGraphic.SetZIndex(100);
				//        }
				//    }
				//}
				MapPoint p = Map.ScreenToMap(e.GetPosition(Map));
				if (!IsValid(p, MapUnits, WrapAround))
					return;
				_feedback.AddPoint(p);
				//_originPoint = _endPoint = p;
				//Polyline line = new Polyline();
				//PointCollection points = new PointCollection {_originPoint, _endPoint};
				//line.Paths.Add(points);
				//_points.Add(_endPoint);
				//if (_points.Count == 2)
				//    _points.Add(_endPoint);
				//_lineCount++;
				//if (Type == MeasureType.Area && _points.Count > 2)
				//{
				//    Polygon poly = new Polygon();
				//    poly.Rings.Add(_points);
				//    GraphicsLayer.Graphics[0].Geometry = poly;
				//}
				//if (Type == MeasureType.Distance)
				//{
				//    if (ShowTotals)
				//    {
				//        Graphic totalTextGraphic = new Graphic()
				//        {
				//            Geometry = _originPoint,
				//            Symbol = new RotatingTextSymbol()
				//            {
				//                OffsetX = -20,
				//                OffsetY = -15,
				//                HorizontalAlignment = HorizontalAlignment.Left
				//            }
				//        };
				//        totalTextGraphic.SetZIndex(100);
				//        GraphicsLayer.Graphics.Add(totalTextGraphic);
				//    }
				//}
				//Graphic marker = new Graphic()
				//{
				//    Geometry = _endPoint,
				//    Symbol = _markerSymbol
				//};
				//GraphicsLayer.Graphics.Add(marker);
				//Graphic lineGraphic = new Graphic()
				//{
				//    Geometry = line,
				//    Symbol = LineSymbol
				//};
				//GraphicsLayer.Graphics.Add(lineGraphic);
				//Graphic textGraphic = new Graphic()
				//{
				//    Geometry = _endPoint,
				//    Symbol = new RotatingTextSymbol()
				//};
				//textGraphic.SetZIndex(1);
				//GraphicsLayer.Graphics.Add(textGraphic);
				_totalLength += _segmentLength;
				//_lengths.Add(_segmentLength);
				_segmentLength = 0;
				_isMeasuring = true;
			}
			_lastClick = pt;
		}

		private static double EuclideanDistance(MapPoint p0, MapPoint p1)
		{
			return Math.Sqrt(Math.Pow(p0.X - p1.X, 2) + Math.Pow(p0.Y - p1.Y, 2));
		}
		private void map_MouseMove(object sender, MouseEventArgs e)
		{
			if (GraphicsLayer.Graphics == null) return;
			Map.Cursor = Cursors.Hand;
			if (_feedback != null && _isMeasuring)
			{
				MapPoint p = Map.ScreenToMap(e.GetPosition(Map));
				if (!IsValid(p, MapUnits, WrapAround))
					return;

				//if (!p.SpatialReference.Equals(_originPoint.SpatialReference)) todo
				//{
				//    // Spatial reference of the map has been changed since the beginning of the measure ==> restart from scratch
				//    IsActivated = false;
				//    IsActivated = true;
				//    return;
				//}
				_feedback.SetPoint(p);

				//int g = GraphicsLayer.Graphics.Count - 1;
				//MapPoint midpoint = new MapPoint((p.X + _originPoint.X) / 2, (p.Y + _originPoint.Y) / 2);
				//PointCollection polypoints = null;
				//if (Type == MeasureType.Area && _points.Count > 2)
				//{
				//    Graphic graphic = GraphicsLayer.Graphics[0];
				//    Polygon poly = graphic.Geometry as Polygon;
				//    polypoints = poly.Rings[0];
				//    int lastPt = polypoints.Count - 1;
				//    polypoints[lastPt] = p;
				//}
				//GraphicsLayer.Graphics[g - 2].Geometry = midpoint;
				//((Polyline) GraphicsLayer.Graphics[g - 1].Geometry).Paths[0][1] = p;
				//GraphicsLayer.Graphics[g].Geometry = midpoint;
				//double angle = Math.Atan2((p.X - _originPoint.X), (p.Y - _originPoint.Y)) / Math.PI * 180 - 90;
				//if (angle > 90 || angle < -90) angle -= 180;
				//RotatingTextSymbol symb = GraphicsLayer.Graphics[g].Symbol as RotatingTextSymbol;
				//symb.Angle = angle;

				//var dist = Length(_originPoint, p);
				//dist = ConvertDistance(dist, DistanceUnits);
				//symb.Text = Convert.ToString(RoundToSignificantDigit(dist));
				//GraphicsLayer.Graphics[g].Symbol = symb;
				//_segmentLength = dist;
				//_tempTotalLength = _totalLength + dist;
				//RotatingTextSymbol totSym;
				//if (Type == MeasureType.Distance)
				//{
				//    totSym = GraphicsLayer.Graphics[0].Symbol as RotatingTextSymbol;
				//    if (totSym != null)
				//    {
				//        totSym.Text = string.Format(FormatString, RoundToSignificantDigit(_tempTotalLength), DistanceUnitToString(DistanceUnits));
				//        GraphicsLayer.Graphics[0].Symbol = totSym;
				//        GraphicsLayer.Graphics[0].Geometry = p;
				//    }
				//}
				//else
				//{
				//    totSym = GraphicsLayer.Graphics[1].Symbol as RotatingTextSymbol;
				//    if (polypoints != null && polypoints.Count > 2)
				//    {
				//        double area;
				//        if (IsWebMercator(p.SpatialReference) || MapUnits == DistanceUnit.DecimalDegrees)
				//        {
				//            // Close the polygon in order to densify the last edge as well
				//            var temppoints = new PointCollection(polypoints);
				//            temppoints.Add(polypoints[0]);
				//            //area = _measureHelper.Area(temppoints, false, IsWebMercator(p.SpatialReference));
				//            var polygon = new Polygon();
				//            polygon.Rings.Add(temppoints);
				//            var proj = new WebMercator();
				//            area = Geodesic.Area((IsWebMercator(p.SpatialReference) ? proj.ToGeographic(polygon) : polygon) as Polygon);
				//        }
				//        else
				//        {
				//            area = 9999;
				//            // todo area = MeasureHelper.GetAreaEuclidean(polypoints) * ConvertDistanceToMeters(1, MapUnits) * ConvertDistanceToMeters(1, MapUnits);
				//        }

				//        area = ConvertAreaUnits(area, this.AreaUnits);

				//        if (totSym != null)
				//        {
				//            totSym.Text = string.Format(FormatString, RoundToSignificantDigit(area), AreaUnitToString(AreaUnits));
				//            GraphicsLayer.Graphics[1].Geometry = p;
				//            GraphicsLayer.Graphics[1].Symbol = totSym;
				//        }
				//    }
				//}
			}

		}

		private double Length(MapPoint point1, MapPoint point2)
		{
			Polyline line = new Polyline();
			line.Paths.Add(new PointCollection { point1, point2 });
			double dist;

			if (IsWebMercator(point2.SpatialReference))
			{
				dist = Geodesic.Length(new WebMercator().ToGeographic(line) as Polyline);
			}
			else if (MapUnits == DistanceUnit.DecimalDegrees)
			{
				dist = Geodesic.Length(line);
			}
			else
			{
				dist = EuclideanDistance(point1, point2);
				dist = ConvertDistanceToMeters(dist, MapUnits);
			}
			return dist;
		}
		public static string AreaUnitToString(AreaUnit unit)
		{
			switch (unit)
			{
				case AreaUnit.SquareKilometers:
					return "km²";
				case AreaUnit.SquareMeters:
					return "m²";
				case AreaUnit.SquareMiles:
					return "mi²";
				case AreaUnit.Acres:
					return "acres";
				case AreaUnit.Hectares:
					return "hectares";
				case AreaUnit.SquareFeet:
					return "feet²";
				default: return string.Empty;
			}
		}
		public static string DistanceUnitToString(DistanceUnit unit)
		{
			switch (unit)
			{
				case DistanceUnit.DecimalDegrees:
					return "°";
				case DistanceUnit.Feet:
					return "feet";
				case DistanceUnit.Kilometers:
					return "km";
				case DistanceUnit.Meters:
					return "m";
				case DistanceUnit.Miles:
					return "mi";
				case DistanceUnit.NauticalMiles:
					return "nm";
				case DistanceUnit.Yards:
					return "yards";
				default: return string.Empty;
			}
		}

		public static double RoundToSignificantDigit(double value)
		{
			return value >= 100 ? Math.Round(value) : RoundToSignificantDigits(value, 3);
		}

		private static double RoundToSignificantDigits(double d, int digits)
		{
			double scale = Math.Pow(10, Math.Floor(Math.Log10(d)) + 1);
			return scale * Math.Round(d / scale, digits);
		}
		public static double ConvertDistanceToMeters(double distance, DistanceUnit fromUnit)
		{
			return distance / ConvertDistance(1, fromUnit);
		}

		public static double ConvertDistance(double distance, DistanceUnit toUnit)
		{
			switch (toUnit)
			{
				case DistanceUnit.Miles:
					return distance / 1609.34;
				case DistanceUnit.Feet:
					return distance * 3.280839895;
				case DistanceUnit.Kilometers:
					return distance * .001;
				case DistanceUnit.Meters:
				case DistanceUnit.Undefined:
					return distance;
				case DistanceUnit.Yards:
					return distance * 1.093613298;
				case DistanceUnit.NauticalMiles:
					return distance / 1852;
				default:
					throw new NotSupportedException(toUnit.ToString());
			}
		}

		public static double ConvertAreaUnits(double area, AreaUnit toUnits)
		{
			switch (toUnits)
			{
				case AreaUnit.Acres:
					return area * 0.000247105381;
				case AreaUnit.SquareMiles:
					return area * 3.86102159E-7;
				case AreaUnit.SquareKilometers:
					return area * 1E-6;
				case AreaUnit.SquareFeet:
					return area * 10.7639104;
				case AreaUnit.Hectares:
					return area * 0.0001;
				case AreaUnit.SquareMeters:
				case AreaUnit.Undefined:
					return area;
				default: //TODO
					throw new NotImplementedException(toUnits.ToString());
			}
		}

		private bool WrapAround
		{
			get { return Map != null && Map.WrapAround; }
		}

		#region ProcessYInfinity
		// Polygons/Polylines including an earth geographic pole have an infinite size in WebMercator coordinates
		// We have to reduce that to a more realistic and non crashing size (arbitrarly third times XMax (~60000000))
		private const double InfiniteY = WebMercatorXMax * 3;
		internal static Polygon ProcessYInfinity(Polygon polygon)
		{
			if (polygon != null)
			{
				foreach (PointCollection pnts in polygon.Rings)
					ProcessYInfinity(pnts);
			}
			return polygon;
		}

		internal static Polyline ProcessYInfinity(Polyline polyline)
		{
			if (polyline != null)
			{
				foreach (PointCollection pnts in polyline.Paths)
					ProcessYInfinity(pnts);
			}
			return polyline;
		}

		private static void ProcessYInfinity(IEnumerable<MapPoint> pnts)
		{
			foreach (MapPoint pnt in pnts)
				ProcessYInfinity(pnt);
		}

		private static void ProcessYInfinity(MapPoint point)
		{
			if (double.IsPositiveInfinity(point.Y))
				point.Y = InfiniteY;
			if (double.IsNegativeInfinity(point.Y))
				point.Y = -InfiniteY;
		}
		#endregion

		internal static bool IsValid(MapPoint p, DistanceUnit mapUnits, bool wrapAround)
		{
			if (p == null)
				return false;

			bool isValid;
			if (wrapAround)
			{
				if (IsWebMercator(p.SpatialReference))
					isValid = true;
				else if (mapUnits == DistanceUnit.DecimalDegrees)
					isValid = p.Y >= -90 && p.Y <= 90;
				else
					isValid = true;
			}
			else
			{
				if (IsWebMercator(p.SpatialReference))
					isValid = p.X >= -WebMercatorXMax && p.X <= WebMercatorXMax;
				else if (mapUnits == DistanceUnit.DecimalDegrees)
					isValid = p.X >= -180 && p.X <= 180 && p.Y >= -90 && p.Y <= 90;
				else
					isValid = true;
			}

			return isValid;
		}

		private static readonly SpatialReference WebMercatorSR = new SpatialReference(102100);
		internal static bool IsWebMercator(SpatialReference spatialReference)
		{
			return WebMercatorSR.Equals(spatialReference);
		}
	}

	internal enum MeasureType
	{
		Distance = 1,
		Area = 2
	}


	internal sealed class RotatingTextSymbol : MarkerSymbol // todo internal? cahnge template?
	{
		private const string Template = @"<ControlTemplate
					xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" 
					xmlns:local=""clr-namespace:{0};assembly={1}"">
						<Grid RenderTransformOrigin=""0.5,0.5"" Width=""200"">
							<Grid.RenderTransform>
								<RotateTransform Angle=""{{Binding Path=Symbol.Angle}}"" />
							</Grid.RenderTransform>
							<TextBlock FontWeight=""Bold"" Foreground=""White"" HorizontalAlignment=""{{Binding Path=Symbol.HorizontalAlignment}}"" VerticalAlignment=""Center""
								Text=""{{Binding Symbol.Text}}"" >
								<TextBlock.Effect><BlurEffect Radius=""5"" /></TextBlock.Effect>
							</TextBlock>
							<TextBlock FontWeight=""Bold"" HorizontalAlignment=""{{Binding Path=Symbol.HorizontalAlignment}}"" VerticalAlignment=""Center""
								Text=""{{Binding Symbol.Text}}"" Foreground=""Black"" />
						</Grid>
					</ControlTemplate>";

		public RotatingTextSymbol()
		{
			Type t = typeof(RotatingTextSymbol);
			var temp = string.Format(Template, t.Namespace, t.Assembly.FullName.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)[0]);
			ControlTemplate = System.Windows.Markup.XamlReader.Load(temp) as ControlTemplate;
			OffsetX = 100;
		}

		/// <summary>
		/// Identifies the <see cref="Angle"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty AngleProperty =
						DependencyProperty.Register("Angle", typeof(double), typeof(RotatingTextSymbol),
						new PropertyMetadata(0.0, OnAnglePropertyChanged));
		/// <summary>
		/// Gets or sets Angle.
		/// </summary>
		public double Angle
		{
			get { return (double)GetValue(AngleProperty); }
			set { SetValue(AngleProperty, value); }
		}

		/// <summary>
		/// AngleProperty property changed handler. 
		/// </summary>
		/// <param name="d">ownerclass that changed its Angle.</param>
		/// <param name="e">DependencyPropertyChangedEventArgs.</param> 
		private static void OnAnglePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			RotatingTextSymbol dp = d as RotatingTextSymbol;
			if (dp != null) dp.OnPropertyChanged("Angle");
		}

		/// <summary>
		/// Identifies the <see cref="Text"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty TextProperty =
						DependencyProperty.Register("Text", typeof(string), typeof(RotatingTextSymbol),
						new PropertyMetadata("", OnTextPropertyChanged));

		/// <summary>
		/// Gets or sets Text.
		/// </summary>
		public string Text
		{
			get { return (string)GetValue(TextProperty); }
			set { SetValue(TextProperty, value); }
		}

		/// <summary>
		/// AngleProperty property changed handler. 
		/// </summary>
		/// <param name="d">ownerclass that changed its Angle.</param>
		/// <param name="e">DependencyPropertyChangedEventArgs.</param> 
		private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			RotatingTextSymbol dp = d as RotatingTextSymbol;
			if (dp != null) dp.OnPropertyChanged("Text");
		}

		public HorizontalAlignment HorizontalAlignment
		{
			get { return (HorizontalAlignment)GetValue(HorizontalAlignmentProperty); }
			set { SetValue(HorizontalAlignmentProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="HorizontalAlignment"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty HorizontalAlignmentProperty =
			DependencyProperty.Register("HorizontalAlignment", typeof(HorizontalAlignment), typeof(RotatingTextSymbol), new PropertyMetadata(HorizontalAlignment.Center));
	}

	internal class Feedback
	{
		private readonly IList<PointCollection> _pcs = new List<PointCollection>();
		private readonly Graphic _lineGraphic;
		private readonly Graphic _fillGraphic;
		private MeasureType _type;
		//private Graphic _fillGraphic;

		public Feedback(MeasureType type) // arg needed? todo
		{
			_type = type;
			_lineGraphic = new Graphic { Geometry = new Polyline() };
			((Polyline)_lineGraphic.Geometry).Paths.Add(new PointCollection());
			if (type == MeasureType.Area)
			{
				_fillGraphic = new Graphic { Geometry = new Polygon() };
				((Polygon)_fillGraphic.Geometry).Rings.Add(new PointCollection());
			}


			//if (type == MeasureType.Distance)
			//{
			//	_lineGraphic = new Graphic { Geometry = new Polyline()};
			//	((Polyline) _lineGraphic.Geometry).Paths.Add(new PointCollection());
			//}
			//else if (type == MeasureType.Area)
			//{
			//	_fillGraphic = new Graphic { Geometry = new Polygon() };
			//	((Polygon)_fillGraphic.Geometry).Rings.Add(new PointCollection());
			//}
			//else
			//{
			//	throw new NotImplementedException();
			//}
		}

		public DistanceUnit MapUnits { get; set; }
		private GraphicsLayer _graphicsLayer;
		public GraphicsLayer GraphicsLayer
		{
			get { return _graphicsLayer; }
			set
			{
				_graphicsLayer = value;
				if (LineSymbol != null)
					_graphicsLayer.Graphics.Add(_lineGraphic);
				if (_fillGraphic != null && FillSymbol != null)
					_graphicsLayer.Graphics.Add(_fillGraphic);
			}
		}

		private LineSymbol _lineSymbol;
		public LineSymbol LineSymbol
		{
			get { return _lineSymbol; }
			set
			{
				_lineSymbol = value;
				_lineGraphic.Symbol = _lineSymbol;
				if (_lineSymbol != null)
				{
					if (!_graphicsLayer.Graphics.Contains(_lineGraphic))
						_graphicsLayer.Graphics.Add(_lineGraphic);
				}
				else
					_graphicsLayer.Graphics.Remove(_lineGraphic);
			}
		}

		private FillSymbol _fillSymbol;
		public FillSymbol FillSymbol
		{
			get { return _fillSymbol; }
			set
			{
				_fillSymbol = value;
				if (_fillGraphic != null)
				{
					_fillGraphic.Symbol = _fillSymbol;
					if (_fillSymbol != null)
					{
						if (!_graphicsLayer.Graphics.Contains(_fillGraphic))
							_graphicsLayer.Graphics.Add(_fillGraphic);
					}
					else
						_graphicsLayer.Graphics.Remove(_fillGraphic);
				}
			}
		}

		public void SetPoint(MapPoint point)
		{
			//var polyline = _graphic.Geometry as Polyline;
			//var polygon = _fillGraphic == null ? null : _fillGraphic.Geometry as Polygon;

			if (_pcs.Any())
			{
				var point1 = _pcs.Last().First();
				var line = new Polyline();
				line.Paths.Add(new PointCollection { point1, point});
				PointCollection pc;
				if (IsWebMercator(_lineGraphic.Geometry.SpatialReference) && false) // todo
				{
					var wgs84Line = new WebMercator().ToGeographic(line);
					//var pol = ((Polyline) new WebMercator().FromGeographic(Geodesic.Densify(wgs84Line, 10000)));
					//pc = pol.Paths[0];
					//Debug.WriteLine("-->" + pol.Paths.Count);
					pc = ((Polyline) new WebMercator().FromGeographic(Geodesic.Densify(wgs84Line, 10000))).Paths[0];

				} else if  (MapUnits == DistanceUnit.DecimalDegrees && false)
				{
					pc = ((Polyline) Geodesic.Densify(line, 10000)).Paths[0];
				}
				else
				{
					pc = new PointCollection {point1, point};
				}
				_pcs[_pcs.Count - 1] = pc;
				pc = new PointCollection(_pcs.SelectMany(p => p));

				var polyline = new Polyline { SpatialReference = _lineGraphic.Geometry.SpatialReference };
				polyline.Paths.Add(pc);
				_lineGraphic.Geometry = ESRI.ArcGIS.Client.Geometry.Geometry.NormalizeCentralMeridian(polyline);

				if (_fillGraphic != null)
				{
					if (pc.Count == 2 && LineSymbol == null)
						pc.Add(pc.First());
					var polygon = new Polygon { SpatialReference = _lineGraphic.Geometry.SpatialReference };
					polygon.Rings.Add(pc);


					// test add another ring todo
					double area = Geodesic.Area((IsWebMercator(polygon.SpatialReference) ? new WebMercator().ToGeographic(polygon) : polygon) as Polygon, 100000000);
					Debug.WriteLine("area ss trous = " + area / 1000000);
					pc.Add(pc.First());
					area = Geodesic.Area((IsWebMercator(polygon.SpatialReference) ? new WebMercator().ToGeographic(polygon) : polygon) as Polygon, 100000000);
					Debug.WriteLine("area ss trous and closed = " + area / 1000000);

					var center = polygon.Extent.GetCenter();
					var pc2 = new PointCollection();
					foreach(var pt in pc)
					{
						var pt2 = new MapPoint((pt.X + center.X) / 2.0, (pt.Y + center.Y) / 2.0);
						pc2.Add(pt2);
					}
					polygon.Rings.Add(pc2);
					area = Geodesic.Area((IsWebMercator(polygon.SpatialReference) ? new WebMercator().ToGeographic(polygon) : polygon) as Polygon, 100000000);
					Debug.WriteLine("area avec trous = " + area/1000000);
					var pc3 = new PointCollection();
					foreach (var pt in pc.Reverse())
					{
						var pt2 = new MapPoint((pt.X + center.X) / 2.0, (pt.Y + center.Y) / 2.0);
						pc3.Add(pt2);
					}
					polygon.Rings.Add(pc3);
					area = Geodesic.Area((IsWebMercator(polygon.SpatialReference) ? new WebMercator().ToGeographic(polygon) : polygon) as Polygon, 100000000);
					Debug.WriteLine("area avec 2 trous = " + area / 1000000);
					_fillGraphic.Geometry = ESRI.ArcGIS.Client.Geometry.Geometry.NormalizeCentralMeridian(polygon);
				}
			}
			else
			{
				_lineGraphic.Geometry.SpatialReference = point.SpatialReference;
				if (_fillGraphic != null)
					_fillGraphic.Geometry.SpatialReference = point.SpatialReference;
			}
		}


		public void AddPoint(MapPoint point)
		{
			// TODO test bidon
			var polygon = new Polygon() {SpatialReference = new SpatialReference(4610)};
			var coords = new List<double>
				             {
105.772508455,37.227470003
,105.772606538,37.227294784
,105.77288799,37.22680087
,105.773093064,37.226438128
,105.77322343,37.226211536
,105.773426735,37.225874497
,105.773827857,37.225148226
,105.774418056,37.224115735
,105.774598947,37.223791334
,105.774656678,37.223689414
,105.774660715,37.223682351
,105.774690101,37.223630954
,105.775031913,37.22303309
,105.775579301,37.222076399
,105.775643933,37.221964851
,105.776239055,37.220901642
,105.77684324,37.219803468
,105.776936135,37.219631037
,105.776972802,37.219562092
,105.776936095,37.219461804
,105.776909294,37.219394821
,105.776840459,37.219330201
,105.776767849,37.219275757
,105.776578798,37.219176911
,105.776087106,37.21895355
,105.775219817,37.218565231
,105.77488435,37.218412167
,105.773766544,37.217965854
,105.773380427,37.218601007
,105.772958436,37.219270304
,105.772714242,37.219692222
,105.7730565,37.219828925
,105.772675975,37.220468052
,105.772463576,37.220434188
,105.77240758,37.22053635
,105.771650385,37.220282163
,105.771479442,37.220555872
,105.771344682,37.220819673
,105.770862862,37.22177162
,105.770856819,37.221826179
,105.77084395,37.222074237
,105.770777321,37.222442236
,105.770744713,37.222695698
,105.770702092,37.222829636
,105.770641378,37.222914447
,105.770596985,37.222982445
,105.770583809,37.223038369
,105.770593188,37.223095466
,105.770610262,37.223152512
,105.770617342,37.22319995
,105.770596281,37.223291539
,105.77052711,37.22340982
,105.770434285,37.223581016
,105.770396741,37.223673592
,105.770275166,37.223829147
,105.770111629,37.223967388
,105.770057242,37.224025778
,105.769911812,37.224166891
,105.769575398,37.22449887
,105.769404388,37.224652542
,105.769094048,37.224899067
,105.768972693,37.224968444
,105.768684274,37.225154099
,105.768564233,37.22524457
,105.768391822,37.225375832
,105.768233732,37.225509639
,105.768111641,37.225615072
,105.767985019,37.225707345
,105.767924872,37.225740271
,105.767811016,37.225790252
,105.767734492,37.225834715
,105.767686954,37.225853034
,105.767633939,37.22589538
,105.767586934,37.225922944
,105.767501803,37.22594559
,105.767386624,37.225973596
,105.767060359,37.226114645
,105.766920236,37.226177107
,105.7666443,37.226293207
,105.766433531,37.226437904
,105.766254231,37.226541068
,105.766195299,37.226585416
,105.766189093,37.226624147
,105.766244499,37.226665997
,105.766295632,37.226720186
,105.766309094,37.226746479
,105.766292965,37.226782636
,105.766171714,37.2269716
,105.766029995,37.227093866
,105.765940149,37.227126103
,105.76587412,37.227121254
,105.765662685,37.226982806
,105.765567338,37.226906041
,105.765549211,37.226853398
,105.765579555,37.226809234
,105.765774054,37.226686627
,105.765799099,37.226660788
,105.765605667,37.226562673
,105.765354322,37.226500985
,105.765295069,37.226513679
,105.765216539,37.226577499
,105.764945654,37.226758634
,105.764615496,37.226950704
,105.764454554,37.227075291
,105.764319074,37.227162341
,105.764107191,37.227306162
,105.763896308,37.227440303
,105.763700047,37.227606884
,105.763244482,37.228122478
,105.763175224,37.22823372
,105.763105664,37.228315068
,105.763064535,37.228380404
,105.763060686,37.228434948
,105.763061135,37.228506832
,105.763087808,37.228752875
,105.763116714,37.229111461
,105.763138706,37.229220357
,105.763174174,37.229384346
,105.76325636,37.229682792
,105.763311483,37.229914583
,105.763355472,37.229969258
,105.763522754,37.230145367
,105.763685021,37.230314913
,105.76388269,37.230505334
,105.76411319,37.230679715
,105.764379467,37.230911902
,105.764587993,37.231088182
,105.764758885,37.231240085
,105.764880842,37.231337783
,105.765009183,37.231414336
,105.765231858,37.231575575
,105.765251669,37.231577206
,105.765299894,37.231561067
,105.765402852,37.231519952
,105.765499036,37.231461293
,105.76567899,37.231312969
,105.765895062,37.231462641
,105.76617034,37.231585321
,105.766583097,37.231799133
,105.76677121,37.231961352
,105.766974102,37.232166485
,105.767176763,37.232348681
,105.767215546,37.232460255
,105.767216345,37.232539101
,105.767214389,37.232611514
,105.767181109,37.232689146
,105.767112799,37.232848725
,105.766744006,37.233524217
,105.766657271,37.233649521
,105.766582674,37.233777965
,105.766471072,37.233928124
,105.766379409,37.234083553
,105.766124855,37.234443615
,105.765852223,37.234857468
,105.765755658,37.234992227
,105.765659941,37.235101804
,105.765512039,37.23521856
,105.765414472,37.235306346
,105.765275781,37.235412524
,105.765140952,37.235555327
,105.765022728,37.23565573
,105.764926806,37.235745236
,105.764781058,37.23587234
,105.764457432,37.23614109
,105.764247222,37.236277211
,105.762851263,37.237340667
,105.762619906,37.237514197
,105.762034824,37.237907201
,105.761831979,37.238063341
,105.761646746,37.238187828
,105.761478594,37.23829387
,105.761550462,37.238396362
,105.761587772,37.238449569
,105.761665445,37.238517658
,105.761795659,37.238560788
,105.762095908,37.238669652
,105.762124892,37.238708157
,105.762125424,37.238760914
,105.762088775,37.238835014
,105.762043665,37.238942584
,105.761993784,37.239013252
,105.761965313,37.239025747
,105.761899257,37.239019136
,105.761826354,37.238987949
,105.761656424,37.238932765
,105.761483186,37.238876722
,105.761375003,37.238836968
,105.761297806,37.23881636
,105.761249505,37.238825464
,105.761210233,37.238857373
,105.761204392,37.238874725
,105.761243395,37.238859884
,105.761301551,37.238848102
,105.761379776,37.238856854
,105.761599547,37.238945015
,105.761927907,37.239060879
,105.762469003,37.239277959
,105.76275767,37.239400869
,105.763119387,37.239554882
,105.763395523,37.239659098
,105.763497466,37.239697573
,105.76386646,37.239862455
,105.764122623,37.239949619
,105.764251747,37.239993556
,105.764371229,37.240044092
,105.76443291,37.24007018
,105.764517528,37.240105969
,105.764609323,37.240145235
,105.764730163,37.240196924
,105.764787719,37.240221691
,105.765181969,37.240391335
,105.765402795,37.240477549
,105.765450454,37.240496052
,105.765865853,37.240657337
,105.766026487,37.240725953
,105.766141172,37.240774942
,105.766147491,37.240777641
,105.76634869,37.240854348
,105.766632464,37.240971214
,105.766760203,37.24102136
,105.766845339,37.2410507
,105.766915027,37.241074714
,105.767034098,37.241133554
,105.767076149,37.241158783
,105.767095042,37.241163149
,105.767099791,37.241135945
,105.767101493,37.241116732
,105.767227025,37.239979001
,105.767246992,37.239818606
,105.767178244,37.239789531
,105.767012648,37.239719496
,105.767007339,37.239717275
,105.766905755,37.239683521
,105.766676225,37.239594227
,105.766565455,37.239551134
,105.766280595,37.239440312
,105.766263088,37.239433443
,105.766183701,37.239402288
,105.766039805,37.239345454
,105.765954018,37.239310851
,105.765913603,37.239297134
,105.765962366,37.239170393
,105.766079928,37.238849686
,105.766358741,37.23811442
,105.766449053,37.237870155
,105.766724376,37.237248801
,105.766790856,37.237103489
,105.767079414,37.236337285
,105.767123843,37.23619879
,105.767170676,37.2360925
,105.767396324,37.236203334
,105.767473121,37.236239835
,105.767546666,37.23627479
,105.767601126,37.236300414
,105.767643294,37.236320255
,105.76785938,37.236421926
,105.768032915,37.236506676
,105.76804748,37.236514162
,105.768137899,37.236560632
,105.768173067,37.236577152
,105.768138603,37.236401761
,105.768124891,37.236299444
,105.768119411,37.236258559
,105.768118005,37.236235371
,105.768106351,37.236043192
,105.768115536,37.235897304
,105.768131498,37.235642357
,105.76818092,37.235368497
,105.768241506,37.235166756
,105.768300099,37.234998754
,105.768409451,37.2347983
,105.768676692,37.234305857
,105.768840357,37.234018636
,105.768943042,37.233830432
,105.769023226,37.233691606
,105.76944326,37.232931752
,105.769618251,37.232627083
,105.769910215,37.232096827
,105.769940174,37.232042414
,105.769962491,37.232004364
,105.76999516,37.231946446
,105.770009764,37.231920558
,105.769941103,37.23189073
,105.769895199,37.231972349
,105.769749169,37.231910818
,105.769537687,37.231813437
,105.769194974,37.231655629
,105.76907636,37.231601011
,105.768843158,37.23149696
,105.768622376,37.231398451
,105.768548916,37.231361911
,105.76853472,37.231355777
,105.768565648,37.231296805
,105.768576089,37.231276445
,105.768025584,37.231032595
,105.767939961,37.230994972
,105.767898776,37.230972294
,105.767857576,37.23094961
,105.76755953,37.23082098
,105.767224639,37.230686397
,105.766527433,37.23044992
,105.766476909,37.230432782
,105.766360432,37.230387988
,105.766244473,37.230339595
,105.76567039,37.230100012
,105.765841358,37.229782603
,105.766489424,37.228645186
,105.767024952,37.228843367
,105.767130848,37.228638852
,105.767203871,37.228499497
,105.767460969,37.228095792
,105.767772578,37.227585053
,105.767946809,37.227304063
,105.768125413,37.227011201
,105.768514178,37.226211703
,105.76851462,37.226210923
,105.768369947,37.226168425
,105.768210562,37.226129942
,105.768100763,37.226116333
,105.768028909,37.226109137
,105.767910176,37.226083312
,105.767830548,37.226067335
,105.767734619,37.226037774
,105.767622572,37.226002497
,105.767595437,37.225998756
,105.767570584,37.225995329
,105.767613725,37.225940226
,105.767691072,37.225890613
,105.767705054,37.22592386
,105.767743358,37.225950069
,105.76778665,37.225960194
,105.768358624,37.226088271
,105.768515369,37.226112026
,105.768631917,37.226130798
,105.76872478,37.226151447
,105.768796904,37.226180845
,105.768933701,37.226232443
,105.769054454,37.226285872
,105.769382041,37.226434067
,105.76968458,37.226571512
,105.769906021,37.22667481
,105.770147948,37.226780484
,105.770228735,37.226822187
,105.770331741,37.226875361
,105.770599318,37.226990579
,105.770927526,37.227129176
,105.770938226,37.227133693
,105.771977696,37.227626497
,105.77217544,37.227323246
,105.772508455,37.227470003





				             };
			var coords2 = new List<double>
				             {
105.76541813,37.22896433
,105.763980327,37.228262939
,105.764012217,37.228211176
,105.764060014,37.228143273
,105.764159636,37.228037262
,105.764300238,37.22786538
,105.764396252,37.22777132
,105.764563998,37.227641013
,105.764741158,37.227519591
,105.764810215,37.227464472
,105.764864576,37.227431319
,105.764895572,37.227423166
,105.764928612,37.227421185
,105.766042317,37.22789673
,105.765541094,37.228755473
,105.765455202,37.228888366
,105.76541813,37.22896433				             };
			var coords3 = new List<double>
				             {
105.770943546,37.223281884
,105.771175286,37.222827129
,105.771548155,37.222204974
,105.771895706,37.222374784
,105.772057817,37.222450644
,105.772205158,37.22252319
,105.772201009,37.222534692
,105.772178092,37.222598412
,105.7717825,37.223339608
,105.771673412,37.223538423
,105.770943546,37.223281884				             };
			var coords4 = new List<double>
				             {
105.765160751,37.229487948
,105.764998176,37.229799051
,105.764993031,37.229835434
,105.765000338,37.229864582
,105.765006437,37.229871685
,105.764744066,37.229777032
,105.764359739,37.229634384
,105.764172558,37.229564021
,105.764126243,37.229532511
,105.764085603,37.229447285
,105.764033494,37.229334301
,105.764000192,37.229237101
,105.763988182,37.229155667
,105.763987761,37.22911392
,105.764012261,37.229077977
,105.764054305,37.229055837
,105.764097864,37.229045262
,105.765160751,37.229487948				             };
			var coords5 = new List<double>
				             {
105.768018455,37.236236508
,105.767956441,37.236241973
,105.767875152,37.236222172
,105.767709953,37.236150031
,105.767831591,37.235985066
,105.767946353,37.235774586
,105.768023719,37.235668078
,105.768045094,37.235656676
,105.768038479,37.235732372
,105.768018886,37.235895219
,105.768015424,37.236082324
,105.768018455,37.236236508				             };

			PointCollection pc = new PointCollection();
			var l = coords;
			for (var i = 0; i < l.Count; i++)
			{
				double x = l[i];
				double y = l[++i];
				pc.Add(new MapPoint(x, y));
			}
			polygon.Rings.Add(pc);

			pc = new PointCollection();
			l = coords2;
			pc.Add(null);
			for (var i = 0; i < l.Count; i++)
			{
				double x = l[i];
				double y = l[++i];
				pc.Add(new MapPoint(x, y));
			}
			pc.Add(null);
			polygon.Rings.Add(pc);

			pc = new PointCollection();
			pc.Add(null);
			l = coords3;
			for (var i = 0; i < l.Count; i++)
			{
				double x = l[i];
				double y = l[++i];
				pc.Add(null);
				pc.Add(new MapPoint(x, y));
			}
			pc.Add(null);
			polygon.Rings.Add(pc);

			pc = new PointCollection();
			l = coords4;
			for (var i = 0; i < l.Count; i++)
			{
				double x = l[i];
				double y = l[++i];
				pc.Add(null);
				pc.Add(new MapPoint(x, y));
			}
			polygon.Rings.Add(pc);

			pc = new PointCollection();
			l = coords5;
			for (var i = 0; i < l.Count; i++)
			{
				double x = l[i];
				double y = l[++i];
				pc.Add(new MapPoint(x, y));
				pc.Add(null);
			}
			polygon.Rings.Add(pc);


			double area = Geodesic.Area(polygon, 10);

			//var geometry = _graphic.Geometry;
			//var polyline = _graphic.Geometry as Polyline;
			//var polygon = _fillGraphic == null ? null : _fillGraphic.Geometry as Polygon;

			SetPoint(point);
			var lastPoint = _pcs.Any() ? _pcs.Last().Last() : point; // may not be in same frame as point
			if (_pcs.Any())
				_pcs.Last().RemoveAt(_pcs.Last().Count - 1); // minor optimization to avoid duplicate points
			_pcs.Add(new PointCollection { lastPoint });
			//if (polygon != null)
			//    if (polygon.Rings.Any())
			//        polygon.Rings[0] = new PointCollection(polyline.Paths.SelectMany(p => p));
			//    else
			//        polygon.Rings.Add(new PointCollection(polyline.Paths.SelectMany(p => p)));
		}

		public void Close()
		{
			bool toRemove = false;
			if (_type == MeasureType.Area)
			{
				if (_pcs.Count < 2 || (_pcs.Count == 2 && _pcs.Last().Count == 1))
					toRemove = true;
				else
					SetPoint(_pcs.First().First()); // close the polygon
			}
			else
			{
				if (_pcs.Count == 1 && _pcs.Last().Count == 1)
					toRemove = true;
			}
			//int count = geometry is Polygon ? (geometry as Polygon).Rings.First().Count : ((Polyline) geometry).Paths.First().Count;
			if (toRemove)
			{
				GraphicsLayer.Graphics.Remove(_lineGraphic);
				GraphicsLayer.Graphics.Remove(_fillGraphic);
			}
//				Debug.WriteLine(" count =" + count + " " + toRemove);
			//Debug.Assert(polyline.Paths.Last().Count == 1);
			//polyline.Paths.Remove(polyline.Paths.Last());
			//				polygon.Rings[0].Add(polygon.Rings[0][0]);
		}

		private static readonly SpatialReference WebMercatorSR = new SpatialReference(102100);
		internal static bool IsWebMercator(SpatialReference spatialReference)
		{
			return WebMercatorSR.Equals(spatialReference);
		}
	}

	//public class MeasureAction : TargetedTriggerAction<Map>
	//{
	//	MeasureImpl m;
	//	//		MeasureRadius radius;
	//	/// <summary>
	//	/// Initializes a new instance of the <see cref="MeasureAction"/> class.
	//	/// </summary>
	//	public MeasureAction()
	//	{
	//		m = new MeasureImpl() { IsActivated = false };
	//		//radius = new MeasureRadius() { IsActivated = false };

	//		LineSymbol = new SimpleLineSymbol()
	//		{
	//			Color = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
	//			Width = 2,
	//			Style = SimpleLineSymbol.LineStyle.Solid
	//		};
	//		FillSymbol = new SimpleFillSymbol()
	//		{
	//			Fill = new SolidColorBrush(Color.FromArgb(0x22, 255, 255, 255)),
	//			BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
	//			BorderThickness = 2
	//		};
	//	}
	//	/// <summary>
	//	/// Called when the target property changes.
	//	/// </summary>
	//	/// <param name="oldTarget">The old target.</param>
	//	/// <param name="newTarget">The new target.</param>
	//	/// <remarks>Override this to hook and unhook functionality on the specified Target, rather than the AssociatedObject.</remarks>
	//	protected override void OnTargetChanged(Map oldTarget, Map newTarget)
	//	{
	//		m.IsActivated = false; // radius.IsActivated = false; // Action must be canceled because the target has changed
	//		OnMapPropertyChanged(oldTarget, newTarget); // Initialize MapUnits from the map
	//		if (newTarget != null)
	//		{
	//			//radius.Map = 
	//			m.Map = newTarget;
	//			base.OnTargetChanged(oldTarget, newTarget);
	//		}
	//	}

	//	/// <summary>
	//	/// Invokes the action.
	//	/// </summary>
	//	/// <param name="parameter">The parameter to the action. If the Action does not require a parameter, the parameter may be set to a null reference.</param>
	//	protected override void Invoke(object parameter)
	//	{
	//		m.ShowTotals = DisplayTotals;
	//		//m.IsActivated = radius.IsActivated = false;
	//		switch (MeasureMode)
	//		{
	//			case Mode.Polygon:
	//				m.Type = MeasureType.Area;
	//				m.IsActivated = true;
	//				break;
	//			case Mode.Polyline:
	//				m.Type = MeasureType.Distance;
	//				m.IsActivated = true;
	//				break;
	//			//case Mode.Radius:
	//			//default:
	//			//    radius.IsActivated = true;
	//			//    break;
	//		}
	//	}

	//	/// <summary>
	//	/// MeasureImpl Action Mode
	//	/// </summary>
	//	public enum Mode
	//	{
	//		/// <summary>
	//		/// Polyline measure
	//		/// </summary>
	//		Polyline,
	//		/// <summary>
	//		/// Polygon measure
	//		/// </summary>
	//		Polygon,
	//		///// <summary>
	//		///// Radius distance measure
	//		///// </summary>
	//		//Radius
	//	}

	//	/// <summary>
	//	/// Gets or sets the measure mode.
	//	/// </summary>
	//	/// <value>The measure mode.</value>
	//	public Mode MeasureMode
	//	{
	//		get { return (Mode)GetValue(MeasureModeProperty); }
	//		set { SetValue(MeasureModeProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="MeasureMode"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty MeasureModeProperty =
	//		DependencyProperty.Register("MeasureMode", typeof(Mode), typeof(MeasureAction), new PropertyMetadata(Mode.Polyline, OnMeasureModePropertyChanged));

	//	private static void OnMeasureModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	//	{
	//		//MeasureAction obj = (MeasureAction)d;
	//		//Mode newValue = (Mode)e.NewValue;
	//		//Mode oldValue = (Mode)e.OldValue;
	//	}

	//	/// <summary>
	//	/// Gets or sets the display area unit used for polygon measurement.
	//	/// </summary>
	//	/// <value>The area units.</value>
	//	public AreaUnit AreaUnit
	//	{
	//		get { return (AreaUnit)GetValue(AreaUnitsProperty); }
	//		set { SetValue(AreaUnitsProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="AreaUnit"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty AreaUnitsProperty =
	//		DependencyProperty.Register("AreaUnit", typeof(AreaUnit), typeof(MeasureAction), new PropertyMetadata(AreaUnit.Undefined, OnAreaUnitsPropertyChanged));

	//	private static void OnAreaUnitsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	//	{
	//		MeasureAction obj = (MeasureAction)d;
	//		obj.m.AreaUnits = (AreaUnit)e.NewValue;
	//	}

	//	/// <summary>
	//	/// Gets or sets the distance unit used for polyline and radius measurement.
	//	/// </summary>
	//	/// <value>The distance units.</value>
	//	public DistanceUnit DistanceUnit
	//	{
	//		get { return (DistanceUnit)GetValue(DistanceUnitProperty); }
	//		set { SetValue(DistanceUnitProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="DistanceUnit"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty DistanceUnitProperty =
	//		DependencyProperty.Register("DistanceUnit", typeof(DistanceUnit), typeof(MeasureAction), new PropertyMetadata(DistanceUnit.Undefined, OnDistanceUnitPropertyChanged));

	//	private static void OnDistanceUnitPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	//	{
	//		MeasureAction obj = (MeasureAction)d;
	//		//obj.m.DistanceUnits = obj.radius.DisplayUnit = (DistanceUnit)e.NewValue;
	//		obj.m.DistanceUnits = (DistanceUnit)e.NewValue;
	//	}

	//	/// <summary>
	//	/// Gets or sets the units of the map.
	//	/// </summary>
	//	/// <remarks>
	//	/// 	If the map units are  not set manually, the measure action will use a default map unit which is calculated from the spatial reference of the map
	//	/// 	or from the <see cref="ESRI.ArcGIS.Client.ArcGISTiledMapServiceLayer.Units"></see> of the layers inside the map.
	//	/// </remarks>
	//	/// <value>The map units.</value>
	//	public DistanceUnit MapUnits
	//	{
	//		get { return (DistanceUnit)GetValue(MapUnitsProperty); }
	//		set { SetValue(MapUnitsProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="MapUnits"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty MapUnitsProperty =
	//		DependencyProperty.Register("MapUnits", typeof(DistanceUnit), typeof(MeasureAction), new PropertyMetadata(DistanceUnit.Undefined, OnMapUnitsPropertyChanged));

	//	private static void OnMapUnitsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	//	{
	//		MeasureAction obj = (MeasureAction)d;
	//		//obj.m.MapUnits = obj.radius.MapUnit = (DistanceUnit)e.NewValue;
	//		obj.m.MapUnits = (DistanceUnit)e.NewValue;
	//	}

	//	/// <summary>
	//	/// Gets or sets the fill symbol used for area and radius measurement.
	//	/// </summary>
	//	/// <value>The fill symbol.</value>
	//	public FillSymbol FillSymbol
	//	{
	//		get { return (FillSymbol)GetValue(FillSymbolProperty); }
	//		set { SetValue(FillSymbolProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="FillSymbol"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty FillSymbolProperty =
	//		DependencyProperty.Register("FillSymbol", typeof(FillSymbol), typeof(MeasureAction), new PropertyMetadata(null, OnFillSymbolPropertyChanged));

	//	private static void OnFillSymbolPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	//	{
	//		MeasureAction obj = (MeasureAction)d;
	//		//obj.radius.FillSymbol = obj.m.FillSymbol = (FillSymbol)e.NewValue;
	//		obj.m.FillSymbol = (FillSymbol)e.NewValue;
	//	}

	//	/// <summary>
	//	/// Gets or sets the line symbol used for polyline measurement.
	//	/// </summary>
	//	/// <value>The line symbol.</value>
	//	public LineSymbol LineSymbol
	//	{
	//		get { return (LineSymbol)GetValue(LineSymbolProperty); }
	//		set { SetValue(LineSymbolProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="LineSymbol"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty LineSymbolProperty =
	//		DependencyProperty.Register("LineSymbol", typeof(LineSymbol), typeof(MeasureAction), new PropertyMetadata(null, OnLineSymbolPropertyChanged));

	//	private static void OnLineSymbolPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	//	{
	//		MeasureAction obj = (MeasureAction)d;
	//		//obj.radius.LineSymbol = obj.m.LineSymbol = (LineSymbol)e.NewValue;
	//		obj.m.LineSymbol = (LineSymbol)e.NewValue;
	//	}

	//	/// <summary>
	//	/// Gets or sets a value indicating whether the total area and distance should be display.
	//	/// </summary>
	//	/// <remarks>This property does not apply to radius measurement.</remarks>
	//	public bool DisplayTotals
	//	{
	//		get { return (bool)GetValue(DisplayTotalsProperty); }
	//		set { SetValue(DisplayTotalsProperty, value); }
	//	}

	//	/// <summary>
	//	/// Identifies the <see cref="DisplayTotals"/> dependency property.
	//	/// </summary>
	//	public static readonly DependencyProperty DisplayTotalsProperty =
	//		DependencyProperty.Register("DisplayTotals", typeof(bool), typeof(MeasureAction), new PropertyMetadata(true));

	//	#region MapUnits Initialization
	//	private void OnMapPropertyChanged(Map oldMap, Map newMap)
	//	{
	//		if (oldMap != null)
	//		{
	//			oldMap.PropertyChanged -= Map_PropertyChanged;
	//		}
	//		if (newMap != null)
	//		{
	//			newMap.PropertyChanged += Map_PropertyChanged;
	//		}
	//		InitializeMapUnit(newMap);
	//	}

	//	private void Map_PropertyChanged(object sender, PropertyChangedEventArgs e)
	//	{
	//		Map map = sender as Map;
	//		if (map != null && e.PropertyName == "SpatialReference")
	//		{
	//			InitializeMapUnit(map);
	//		}
	//	}


	//	private static readonly SpatialReference _webMercSref = new SpatialReference(102100);
	//	/// <summary>
	//	/// Try to initialize the map units
	//	/// </summary>
	//	private void InitializeMapUnit(Map map)
	//	{
	//		if (map == null || map.SpatialReference == null)
	//			return;

	//		// First test the well know spatial references
	//		if (map.SpatialReference.WKID == 4326)
	//			MapUnits = DistanceUnit.DecimalDegrees;
	//		else if (_webMercSref.Equals(map.SpatialReference))
	//			MapUnits = DistanceUnit.Meters;
	//		else
	//		{
	//			Layer layer = map.Layers == null ? null :
	//				map.Layers.FirstOrDefault(l => l.SpatialReference != null && l.SpatialReference.Equals(map.SpatialReference));

	//			string layerUnits;
	//			if (layer is ArcGISDynamicMapServiceLayer)
	//			{
	//				layerUnits = ((ArcGISDynamicMapServiceLayer)layer).Units;
	//			}
	//			else if (layer is ArcGISTiledMapServiceLayer)
	//			{
	//				layerUnits = ((ArcGISTiledMapServiceLayer)layer).Units;
	//			}
	//			else
	//				layerUnits = null;

	//			if (!string.IsNullOrEmpty(layerUnits))
	//			{
	//				// Remove leading 'esri' to layerUnits
	//				if (layerUnits.StartsWith("esri"))
	//					layerUnits = layerUnits.Substring(4);

	//				try
	//				{
	//					DistanceUnit unit = (DistanceUnit)Enum.Parse(typeof(DistanceUnit), layerUnits, true);
	//					MapUnits = unit;
	//				}
	//				catch (ArgumentException) // layersUnits is not one of the named constants defined for the enumeration
	//				{
	//				}
	//			}
	//		}
	//	}

	//	#endregion

	//}


}
