using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Toolkit;

namespace ArcGISSilverlightSDK
{
    public partial class AutoTimeSliderSample : UserControl
    {
        public AutoTimeSliderSample()
        {
            InitializeComponent();
        }
    }

    // Subclass TimeSlider that automates the slider visibility and the slider time range depending on visible layers
    public class AutoTimeSlider : TimeSlider
    {
        public AutoTimeSlider()
        {
            ValueChanged += AutoTimeSlider_ValueChanged;
            Unloaded += AutoTimeSlider_Unloaded;
        }

        void AutoTimeSlider_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPlaying = false;
            Unloaded -= AutoTimeSlider_Unloaded;
        }

        void AutoTimeSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            SetFormattedExtent();
            if (Map != null)
                Map.TimeExtent = e.NewValue;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            IsPlaying = true;
        }

        public Map Map
        {
            get { return (Map)GetValue(MapProperty); }
            set { SetValue(MapProperty, value); }
        }

        public static readonly DependencyProperty MapProperty =
            DependencyProperty.Register("Map", typeof(Map), typeof(AutoTimeSlider), new PropertyMetadata(null, OnMapChanged));

        public static void OnMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AutoTimeSlider)d).OnMapChanged(e.OldValue as Map, e.NewValue as Map);
        }
        public void OnMapChanged(Map oldValue, Map newValue)
        {
            if (oldValue != null)
            {
                DetachLayersHandler(oldValue.Layers);
            }

            if (newValue != null)
            {
                AttachLayersHandler(newValue.Layers);
                InitTimeSlider();
                newValue.TimeExtent = Value;
            }
        }

        public string FormattedMinimumValue
        {
            get { return (string)GetValue(FormattedMinimumValueProperty); }
            set { SetValue(FormattedMinimumValueProperty, value); }
        }
        public static readonly DependencyProperty FormattedMinimumValueProperty =
            DependencyProperty.Register("FormattedMinimumValue", typeof(string), typeof(AutoTimeSlider), null);

        public string FormattedMaximumValue
        {
            get { return (string)GetValue(FormattedMaximumValueProperty); }
            set { SetValue(FormattedMaximumValueProperty, value); }
        }
        public static readonly DependencyProperty FormattedMaximumValueProperty =
            DependencyProperty.Register("FormattedMaximumValue", typeof(string), typeof(AutoTimeSlider), null);

        public string FormattedExtent
        {
            get { return (string)GetValue(FormattedExtentProperty); }
            set { SetValue(FormattedExtentProperty, value); }
        }
        public static readonly DependencyProperty FormattedExtentProperty =
            DependencyProperty.Register("FormattedExtent", typeof(string), typeof(AutoTimeSlider), null);

        private void SetFormattedMinimumValue()
        {
            FormattedMinimumValue = MinimumValue.ToString(_interval != null ? _interval.DateFormat : null);
        }
        private void SetFormattedMaximumValue()
        {
            FormattedMaximumValue = MaximumValue.ToString(_interval != null ? _interval.DateFormat : null);
        }
        private void SetFormattedExtent()
        {
            string format = _interval != null ? _interval.ExtentFormat ?? _interval.DateFormat : null;
            switch (TimeMode)
            {
                case TimeMode.TimeInstant:
                    FormattedExtent = Value.Start.ToString(format);
                    break;
                case TimeMode.CumulativeFromStart:
                    FormattedExtent = "--> " + Value.Start.ToString(format);
                    break;
                case TimeMode.TimeExtent:
                    FormattedExtent = Value.Start.ToString(format) + " --> " + Value.End.ToString(format);
                    break;
            }
        }

        private void InitTimeSlider()
        {
            if (Map != null)
                InitTimeSlider(GetTimeExtent(Map.Layers));
        }

        private static TimeExtent GetTimeExtent(IEnumerable<Layer> layers)
        {
            if (layers == null)
                return null;

            TimeExtent timeExtent = null;
            foreach (var layer in layers)
            {
                if (!layer.IsInitialized || layer.InitializationFailure != null || !layer.Visible)
                    continue;

                TimeExtent te = GetTimeExtent(layer);
                if (te == null)
                {
                    // FeatureLayer doesn"t expose its TimeExtent --> need to get it after update from graphics
                    if (layer is ArcGISDynamicMapServiceLayer)
                        te = ((ArcGISDynamicMapServiceLayer)layer).TimeExtent;
                    else if (layer is ArcGISImageServiceLayer)
                        te = ((ArcGISImageServiceLayer)layer).TimeExtent;
                    else if (layer is GraphicsLayer)
                        te = GetTimeExtent(layer as GraphicsLayer);
                    else if (layer is ElementLayer)
                        te = GetTimeExtent(layer as ElementLayer);
                }

                if (te != null)
                {
                    if (timeExtent == null)
                    {
                        timeExtent = new TimeExtent(te.Start, te.End);
                        if (timeExtent.Start == DateTime.MinValue)
                            timeExtent.Start = timeExtent.End;
                        if (timeExtent.End == DateTime.MaxValue)
                            timeExtent.End = timeExtent.Start;
                    }
                    else
                    {
                        DateTime start = te.Start == DateTime.MinValue ? te.End : te.Start;
                        if (timeExtent.Start > start)
                            timeExtent.Start = start;

                        DateTime end = te.End == DateTime.MaxValue ? te.Start : te.End;
                        if (timeExtent.End < end)
                            timeExtent.End = end;
                    }
                }
            }
            return timeExtent;
        }

        private static TimeExtent GetTimeExtent(GraphicsLayer graphicsLayer)
        {
            if (graphicsLayer.Graphics == null)
                return null;

            TimeExtent timeExtent = null;
            foreach (var graphic in graphicsLayer.Graphics)
            {
                TimeExtent te = graphic.TimeExtent;

                if (te != null)
                {
                    if (timeExtent == null)
                    {
                        timeExtent = new TimeExtent(te.Start, te.End);
                        if (timeExtent.Start == DateTime.MinValue)
                            timeExtent.Start = timeExtent.End;
                        if (timeExtent.End == DateTime.MaxValue)
                            timeExtent.End = timeExtent.Start;
                    }
                    else
                    {
                        DateTime start = te.Start == DateTime.MinValue ? te.End : te.Start;
                        if (timeExtent.Start > start)
                            timeExtent.Start = start;

                        DateTime end = te.End == DateTime.MaxValue ? te.Start : te.End;
                        if (timeExtent.End < end)
                            timeExtent.End = end;
                    }
                }
            }
            return timeExtent;
        }

        private static TimeExtent GetTimeExtent(ElementLayer elementLayer)
        {
            if (elementLayer.Children == null)
                return null;

            TimeExtent timeExtent = null;
            foreach (var element in elementLayer.Children)
            {
                TimeExtent te = ElementLayer.GetTimeExtent(element);

                if (te != null)
                {
                    if (timeExtent == null)
                    {
                        timeExtent = new TimeExtent(te.Start, te.End);
                        if (timeExtent.Start == DateTime.MinValue)
                            timeExtent.Start = timeExtent.End;
                        if (timeExtent.End == DateTime.MaxValue)
                            timeExtent.End = timeExtent.Start;
                    }
                    else
                    {
                        DateTime start = te.Start == DateTime.MinValue ? te.End : te.Start;
                        if (timeExtent.Start > start)
                            timeExtent.Start = start;

                        DateTime end = te.End == DateTime.MaxValue ? te.Start : te.End;
                        if (timeExtent.End < end)
                            timeExtent.End = end;
                    }
                }
            }
            return timeExtent;
        }

        private static TimeExtent GetTimeExtent(Layer layer)
        {
            TimeExtent timeExtent = layer.VisibleTimeExtent;
            return timeExtent ?? (layer is GroupLayer ? GetTimeExtent((layer as GroupLayer).ChildLayers) : null);
        }

        private Interval _interval;

        private void InitTimeSlider(TimeExtent timeExtent)
        {
            var timeSlider = this;
            if (timeExtent == null)
            {
                timeSlider.Visibility = Visibility.Collapsed;
                timeSlider.IsPlaying = false;
                return;
            }

            if (timeSlider.MinimumValue == timeExtent.Start && timeSlider.MaximumValue == timeExtent.End && timeSlider.Visibility == Visibility.Visible)
                return; // no modifs

            timeSlider.MinimumValue = timeExtent.Start;
            timeSlider.MaximumValue = timeExtent.End;

            TimeSpan fullExtent = timeExtent.End - timeExtent.Start;

            _interval = new Interval(fullExtent);
            var intervals = new List<DateTime>();
            DateTime dt = timeSlider.MinimumValue;
            while (dt < timeSlider.MaximumValue)
            {
                intervals.Add(dt);
                dt = _interval.AddInterval(dt);
            }
            intervals.Add(timeSlider.MaximumValue);

            timeSlider.Intervals = intervals;

            // Rewind
            if (TimeMode == TimeMode.TimeExtent && timeSlider.Intervals.Count() > 1)
            {
                timeSlider.Value = new TimeExtent(timeSlider.MinimumValue, timeSlider.Intervals.Skip(1).First());
            }
            else
            {
                timeSlider.Value = new TimeExtent(timeSlider.MinimumValue);
            }
            SetFormattedMinimumValue();
            SetFormattedMaximumValue();
            SetFormattedExtent();
            if (timeSlider.Visibility != Visibility.Visible)
            {
                timeSlider.Visibility = Visibility.Visible;
                IsPlaying = true;
            }
        }

        // Helper class to manage interval having much sense (entire number of years, months, days, ...)
        private class Interval
        {
            private const int MaxInterval = 100;
            private const int MinInterval = 25;

            public Interval(TimeSpan fullExtent)
            {
                double totaldays = fullExtent.TotalDays;
                double totalmonths = totaldays / 30; // approximation but that doesn't matter
                double totalyears = totaldays / 365;

                if (totalyears > MinInterval)
                {
                    // Step by years
                    Years = (int)Math.Ceiling(totalyears / MaxInterval);
                    DateFormat = "yyyy";
                }
                else if (totalmonths > MinInterval)
                {
                    // Step by Months
                    Months = (int)Math.Ceiling(totalmonths / MaxInterval);
                    DateFormat = "yyyy/MM";
                    ExtentFormat = "Y"; // Year Month pattern
                }
                else if (totaldays > MinInterval)
                {
                    // Step by Days
                    TimeSpan = TimeSpan.FromDays((int)Math.Ceiling(totaldays / MaxInterval));
                    DateFormat = "d"; // short date pattern
                    ExtentFormat = "D";
                }
                else if (fullExtent.TotalHours > MinInterval)
                {
                    // Step by Hours
                    TimeSpan = TimeSpan.FromHours((int)Math.Ceiling(fullExtent.TotalHours / MaxInterval));
                    ExtentFormat = "t";
                }
                else if (fullExtent.TotalMinutes > MinInterval)
                {
                    // Step by Minutes
                    TimeSpan = TimeSpan.FromMinutes((int)Math.Ceiling(fullExtent.TotalMinutes / MaxInterval));
                    ExtentFormat = "t";
                }
                else if (fullExtent.TotalSeconds > MinInterval)
                {
                    // Step by Seconds
                    TimeSpan = TimeSpan.FromSeconds((int)Math.Ceiling(fullExtent.TotalSeconds / MaxInterval));
                    ExtentFormat = "T";
                }
                else
                {
                    // Give up
                    TimeSpan = new TimeSpan(fullExtent.Ticks / MaxInterval);
                    ExtentFormat = "T";
                }
            }
            int Years { get; set; }
            int Months { get; set; }
            internal string DateFormat { get; set; }
            internal string ExtentFormat { get; set; }

            private TimeSpan TimeSpan { get; set; }

            public DateTime AddInterval(DateTime dt)
            {
                if (Years > 0)
                    return dt.AddYears(Years);
                if (Months > 0)
                    return dt.AddMonths(Months);
                return dt + TimeSpan;
            }
        }

        #region Map Event Handlers


        private void DetachLayersHandler(LayerCollection layers)
        {
            if (layers != null)
            {
                layers.LayersInitialized -= InitTimeSliderHandler; // not sure we had subscribe to it. Weird lazy way to get it done :-(
                layers.CollectionChanged -= LayerCollectionChangedHandler;
                foreach (Layer layer in layers)
                    DetachLayerHandler(layer);
            }
        }

        private void AttachLayersHandler(LayerCollection layers)
        {
            if (layers != null)
            {
                layers.CollectionChanged += LayerCollectionChangedHandler;

                // if one layer is not initialized, subscribe to Layers_Initialize event in order to update attribution items
                // This update is only useful to avoid blank lines (for any reason, a TextBlock with a null string has a null height the first time and a non null height after setting again the text value to null)
                if (!layers.All(layer => layer.IsInitialized))
                    layers.LayersInitialized += InitTimeSliderHandler;
                foreach (Layer layer in layers)
                    AttachLayerHandler(layer);
            }
        }

        private void AttachLayerHandler(Layer layer)
        {
            layer.PropertyChanged += layer_PropertyChanged;
            if (layer is GroupLayer)
                ((GroupLayer)layer).ChildLayers.CollectionChanged += LayerCollectionChangedHandler;
            else if (layer is FeatureLayer)
                ((FeatureLayer)layer).UpdateCompleted += InitTimeSliderHandler;
        }

        private void DetachLayerHandler(Layer layer)
        {
            layer.PropertyChanged -= layer_PropertyChanged;
            if (layer is GroupLayer)
                ((GroupLayer)layer).ChildLayers.CollectionChanged -= LayerCollectionChangedHandler;
            else if (layer is FeatureLayer)
                ((FeatureLayer)layer).UpdateCompleted -= InitTimeSliderHandler;
        }

        private void layer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Visible")
                InitTimeSlider();
        }

        void InitTimeSliderHandler(object sender, EventArgs args)
        {
            InitTimeSlider();
        }

        private void LayerCollectionChangedHandler(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var oldItems = e.OldItems;
            var newItems = e.NewItems;
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                newItems = Map.Layers;
            if (oldItems != null)
                foreach (var item in oldItems)
                    DetachLayerHandler(item as Layer);
            if (newItems != null)
                foreach (var item in newItems)
                    AttachLayerHandler(item as Layer);
            InitTimeSlider();
        }
        #endregion
    }

    public class PlaySpeedLogConverter : IValueConverter
    {
        private static readonly TimeSpan ReferenceSpeed = TimeSpan.FromMilliseconds(1000);
        private const double Exp = 1.5;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(TimeSpan))
            {
                double val = -System.Convert.ToDouble(value);
                var ticks = (long)(ReferenceSpeed.Ticks * Math.Pow(Exp, val));
                return TimeSpan.FromTicks(ticks);
            }
            if (value is TimeSpan)
            {
                var d = ((TimeSpan)value);
                long ticks = d.Ticks;
                double ratio = (double)ticks / ReferenceSpeed.Ticks;
                return -Math.Log(ratio) / Math.Log(Exp);
            }
            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}
