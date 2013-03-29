using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace ArcGISSilverlightSDK
{
    public partial class PieChartMaptips : UserControl
    {
        public PieChartMaptips()
        {
            InitializeComponent();
        }
    }

    // Converter from the attributes dictionary to an enumeration of Key-Value to use as ItemsSource of the chart
    public class ItemsSourceConverter : System.Windows.Data.IValueConverter
    {
        public PieElements PieElements { get; set; } // List of Fields to show in the chart

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var attributes = value as IDictionary<string, object>;
            return PieElements.Select(elt => new KeyValuePair<string, object>(elt.DisplayName, attributes[elt.FieldName]));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }
    }

    public class PieElements : ObservableCollection<PieElement> { }

    public class PieElement
    {
        public string FieldName { get; set; }
        public string DisplayName { get; set; }
    }
}
