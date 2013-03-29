using System;
using System.Windows;
using System.Windows.Controls;
using ESRI.ArcGIS.Client.Toolkit;

namespace ArcGISSilverlightSDK
{
    public partial class ScaleAsText : UserControl
    {
        public ScaleAsText()
        {
            InitializeComponent();
        }
    }

    public class ScaleLineAsText : ScaleLine
    {
        public ScaleLineAsText()
        {
            DefaultStyleKey = typeof(ScaleLine);
            PropertyChanged += ScaleLineAsTextPropertyChanged;
        }

        private void ScaleLineAsTextPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            CalculateOutValue();
        }

        public ScaleLineUnit InUnit
        {
            get { return (ScaleLineUnit)GetValue(InUnitProperty); }
            set { SetValue(InUnitProperty, value); }
        }

        // Using a DependencyProperty as the backing store for InUnit.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InUnitProperty =
            DependencyProperty.Register("InUnit", typeof(ScaleLineUnit), typeof(ScaleLineAsText), new PropertyMetadata(ScaleLineUnit.Centimeters, OnInUnitChanged));

        private static void OnInUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scaleLine = d as ScaleLineAsText;
            if (scaleLine != null)
            {
                scaleLine.CalculateOutValue();
            }
        }


        public ScaleLineUnit OutUnit
        {
            get { return (ScaleLineUnit)GetValue(OutUnitProperty); }
            internal set { SetValue(OutUnitProperty, value); }
        }

        // Using a DependencyProperty as the backing store for InUnit.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OutUnitProperty =
            DependencyProperty.Register("OutUnit", typeof(ScaleLineUnit), typeof(ScaleLineAsText), null);



        public double OutValue
        {
            get { return (double)GetValue(OutValueProperty); }
            private set { SetValue(OutValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for OutValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OutValueProperty =
            DependencyProperty.Register("OutValue", typeof(double), typeof(ScaleLineAsText), null);

        private void CalculateOutValue()
        {

            // Calculate TargetWidth to get best out units
            double targetWidth = (double)InUnit * 96 / (double)ScaleLineUnit.Inches;

            if (targetWidth != TargetWidth)
            {
                Dispatcher.BeginInvoke(() => { TargetWidth = targetWidth; });
            }
            else
            {
                bool isMetric = Enum.GetName(typeof (ScaleLineUnit), InUnit).IndexOf("meter", StringComparison.OrdinalIgnoreCase) >= 0;

                double inSize = isMetric ? MetricSize : USSize;
                double outValue = isMetric ? MetricValue : USValue;
                OutUnit = isMetric ? MetricUnit : USUnit;

                outValue *= (double)InUnit * 96 / (double)ScaleLineUnit.Inches / inSize ;
                OutValue = RoundToSignificantDigit(outValue);
            }
        }

        internal static double RoundToSignificantDigit(double value)
        {
            return value >= 100 ? Math.Round(value) : RoundToSignificantDigits(value, 3);
        }

        internal static double RoundToSignificantDigits(double d, int digits)
        {
            double scale = Math.Pow(10, Math.Floor(Math.Log10(d)) + 1);
            return scale * Math.Round(d / scale, digits);
        }

    }

}
