#nullable disable

using System;
using System.Globalization;
using System.Windows.Data;


namespace RfLab.Converters
{
    #region BoolToVisibilityConverter
    // ========================================================================
    /// 
    /// \addtogroup BoolToVisibilityConverter
    /// 
    // ========================================================================
    //@{

    [ValueConversion(typeof(bool), typeof(System.Windows.Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if ((value == null) || !(value is bool))
                return System.Windows.Visibility.Collapsed;
            if ((bool)value)
                return System.Windows.Visibility.Visible;
            else
                return System.Windows.Visibility.Collapsed;
        }

        public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    //@}
    // ========================================================================
    #endregion // BoolToVisibilityConverter


    #region BoolToNoVisibilityConverter
    // ========================================================================
    /// 
    /// \addtogroup BoolToNoVisibilityConverter
    /// 
    // ========================================================================
    //@{

    [ValueConversion(typeof(bool), typeof(System.Windows.Visibility))]
    public class BoolToNoVisibilityConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if ((value == null) || !(value is bool))
                return System.Windows.Visibility.Visible;
            if ((bool)value)
                return System.Windows.Visibility.Collapsed;
            else
                return System.Windows.Visibility.Visible;
        }

        public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    //@}
    // ========================================================================
    #endregion // BoolToNoVisibilityConverter


    #region NullToVisibilityConverter
    // ========================================================================
    /// 
    /// \addtogroup NullToVisibilityConverter
    /// 
    // ========================================================================
    //@{

    [ValueConversion(typeof(object), typeof(System.Windows.Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if (value == null)
                return System.Windows.Visibility.Collapsed;
            else
                return System.Windows.Visibility.Visible;
        }

        public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    //@}
    // ========================================================================
    #endregion // NullToVisibilityConverter
}
