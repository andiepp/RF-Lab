#nullable disable

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RfLab.Converters
{
    [ValueConversion(typeof(int), typeof(bool))]
    public class DDSModModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((string)parameter == ((int)value).ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is true && int.TryParse((string)parameter, out int mode) ? mode : Binding.DoNothing;
        }
    }
}
