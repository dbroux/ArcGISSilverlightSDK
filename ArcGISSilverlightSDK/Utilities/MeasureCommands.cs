using System;
using System.Windows.Input;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Symbols;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ArcGISSilverlightSDK
{
	public class MeasureCommands : DependencyObject
	{
		private readonly MeasureImpl _measureImpl;
		private readonly ToolPool _toolPool;

		public MeasureCommands()
		{
			_measureImpl = new MeasureImpl() { IsActivated = false }; // todo isactivated exposed??
			_measureImpl.MeasureCompleted += (s, e) =>
			{
				_toolPool.ActiveTool = null;
				_clearGraphics.RaiseCanExecuteChanged();
				_cancelMeasure.RaiseCanExecuteChanged();
			};
			_toolPool = new ToolPool();

			// todo authorize null?
			LineSymbol = new SimpleLineSymbol
				             {
				Color = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
				Width = 2,
				Style = SimpleLineSymbol.LineStyle.Solid
			};
			FillSymbol = new SimpleFillSymbol
				             {
				Fill = new SolidColorBrush(Color.FromArgb(0x22, 255, 255, 255)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
				BorderThickness = 2
			};

		}

		/// <summary>
		/// Gets or sets the fill symbol used for area and radius measurement.
		/// </summary>
		/// <value>The fill symbol.</value>
		public FillSymbol FillSymbol
		{
			get { return (FillSymbol)GetValue(FillSymbolProperty); }
			set { SetValue(FillSymbolProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="FillSymbol"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty FillSymbolProperty =
			DependencyProperty.Register("FillSymbol", typeof(FillSymbol), typeof(MeasureCommands), new PropertyMetadata(null, OnFillSymbolPropertyChanged));

		private static void OnFillSymbolPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((MeasureCommands)d)._measureImpl.FillSymbol = (FillSymbol)e.NewValue;
		}

		/// <summary>
		/// Gets or sets the line symbol used for polygon or polyline measurement.
		/// </summary>
		/// <value>The line symbol.</value>
		public LineSymbol LineSymbol
		{
			get { return (LineSymbol)GetValue(LineSymbolProperty); }
			set { SetValue(LineSymbolProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="LineSymbol"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LineSymbolProperty =
			DependencyProperty.Register("LineSymbol", typeof(LineSymbol), typeof(MeasureCommands), new PropertyMetadata(null, OnLineSymbolPropertyChanged));

		private static void OnLineSymbolPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((MeasureCommands)d)._measureImpl.LineSymbol = (LineSymbol)e.NewValue;
		}


		public Map Map
		{
			get { return (Map)GetValue(MapProperty); }
			set { SetValue(MapProperty, value); }
		}

		// Using a DependencyProperty as the backing store for Map.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty MapProperty =
			DependencyProperty.Register("Map", typeof(Map), typeof(MeasureCommands), new PropertyMetadata(null, OnMapChanged));

		private static void OnMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((MeasureCommands)d)._measureImpl.Map = (Map)e.NewValue;
		}


		public GraphicsLayer GraphicsLayer { get; set; }

		#region MeasurePolyline Tool
		private DelegateTool _measurePolyline;
		public ITool MeasurePolyline
		{
			get { return _measurePolyline ?? (_measurePolyline = new DelegateTool(OnMeasurePolyline, OnMeasurePolylineCanExecute, OnMeasurePolylineIsActive)); }
		}

		private void OnMeasurePolyline(object parameter)
		{
			if (!OnMeasurePolylineCanExecute(parameter))
				return;

			_toolPool.ActiveTool = _measurePolyline;
			_measureImpl.Type = MeasureType.Distance;
			_measureImpl.IsActivated = true;
			_cancelMeasure.RaiseCanExecuteChanged();
			// todo _measureImpl.ShowTotals = ??
		}

		private bool OnMeasurePolylineCanExecute(object parameter)
		{
			return _toolPool.ActiveTool != _measurePolyline;
		}

		private bool OnMeasurePolylineIsActive(object parameter)
		{
			return _toolPool.IsActive(_measurePolyline);
		}
		#endregion

		#region MeasurePolygon Tool
		private DelegateTool _measurePolygon;
		public ITool MeasurePolygon
		{
			get { return _measurePolygon ?? (_measurePolygon = new DelegateTool(OnMeasurePolygon, OnMeasurePolygonCanExecute, OnMeasurePolygonIsActive)); }
		}

		private void OnMeasurePolygon(object parameter)
		{
			if (OnMeasurePolygonCanExecute(parameter))
			{
				_toolPool.ActiveTool = _measurePolygon;
				_measureImpl.Type = MeasureType.Area;
				_measureImpl.IsActivated = true;
				_cancelMeasure.RaiseCanExecuteChanged();
			}
		}

		private bool OnMeasurePolygonCanExecute(object parameter)
		{
			return _toolPool.ActiveTool != _measurePolygon; // todo ou == null??
		}

		private bool OnMeasurePolygonIsActive(object parameter)
		{
			return _toolPool.IsActive(_measurePolygon);
		}

		#endregion

		#region CancelMeasure Command
		private DelegateCommand _cancelMeasure;
		public ICommand CancelMeasure
		{
			get { return _cancelMeasure ?? (_cancelMeasure = new DelegateCommand(OnCancelMeasure, OnCancelMeasureCanExecute)); }
		}

		private void OnCancelMeasure(object parameter)
		{
			_toolPool.ActiveTool = null;
			_measureImpl.IsActivated = false;
			_cancelMeasure.RaiseCanExecuteChanged();
		}

		private bool OnCancelMeasureCanExecute(object parameter)
		{
			return _toolPool.ActiveTool != null;
		}
		#endregion

		#region ClearGraphics Command
		private DelegateCommand _clearGraphics;
		public ICommand ClearGraphics
		{
			get { return _clearGraphics ?? (_clearGraphics = new DelegateCommand(OnClearGraphics, OnClearGraphicsCanExecute)); }
		}

		private void OnClearGraphics(object parameter)
		{
			if (!OnClearGraphicsCanExecute(parameter))
				return;
			OnCancelMeasure(parameter);
			GraphicsLayer.ClearGraphics();
			_clearGraphics.RaiseCanExecuteChanged();
		}

		private bool OnClearGraphicsCanExecute(object parameter)
		{
			return GraphicsLayer != null && GraphicsLayer.Graphics.Any();
		}
		#endregion


		private class DelegateCommand : ICommand
		{
			private readonly Predicate<object> _canExecute;
			private readonly Action<object> _method;
			public event EventHandler CanExecuteChanged;

			public DelegateCommand(Action<object> method, Predicate<object> canExecute)
			{
				_method = method;
				_canExecute = canExecute;
			}

			public bool CanExecute(object parameter)
			{
				return _canExecute == null || _canExecute(parameter);
			}

			public void Execute(object parameter)
			{
				_method.Invoke(parameter);
			}

			protected virtual void OnCanExecuteChanged(EventArgs e)
			{
				var canExecuteChanged = CanExecuteChanged;

				if (canExecuteChanged != null)
					canExecuteChanged(this, e);
			}

			public void RaiseCanExecuteChanged()
			{
				OnCanExecuteChanged(EventArgs.Empty);
			}
		}

		private class DelegateTool : DelegateCommand, ITool
		{
			private readonly Predicate<object> _isActive;
			public event EventHandler IsActiveChanged;

			public DelegateTool(Action<object> method, Predicate<object> canExecute, Predicate<object> isActive)
				: base(method, canExecute)
			{
				_isActive = isActive;
			}

			public bool IsActive(object parameter)
			{
				return _isActive != null && _isActive(parameter);
			}

			protected virtual void OnIsActiveChanged(EventArgs e)
			{
				var isActiveChanged = IsActiveChanged;

				if (isActiveChanged != null)
					isActiveChanged(this, e);
			}

			public void RaiseIsActiveChanged()
			{
				OnIsActiveChanged(EventArgs.Empty);
			}
		}

		private class ToolPool
		{
			private DelegateTool _activeTool;

			public DelegateTool ActiveTool
			{
				get { return _activeTool; }
				set
				{
					if (value != _activeTool)
					{
						var previousTool = _activeTool;
						_activeTool = value;
						if (previousTool != null)
						{
							previousTool.RaiseCanExecuteChanged();
							previousTool.RaiseIsActiveChanged();
						}
						if (_activeTool != null)
						{
							_activeTool.RaiseCanExecuteChanged();
							_activeTool.RaiseIsActiveChanged();
						}
					}
				}
			}

			public bool IsActive(DelegateTool tool)
			{
				return tool == _activeTool;
			}
		}

	}


	public interface ITool : ICommand
	{
		// Summary:
		//     Occurs when changes occur that affect whether the tool is active.
		event EventHandler IsActiveChanged;

		// Summary:
		//     Defines the method that determines whether the tool is active in its
		//     current state.
		//
		// Parameters:
		//   parameter:
		//     Data used by the tool. If the tool does not require data to be passed,
		//     this object can be set to null.
		//
		// Returns:
		//     true if this tool ias active; otherwise, false.
		bool IsActive(object parameter);
	}
}
