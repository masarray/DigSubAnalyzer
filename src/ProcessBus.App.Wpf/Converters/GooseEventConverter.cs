using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ProcessBus.App.Wpf.Converters
{
    public class GooseEventBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value?.ToString() ?? "";

            return text switch
            {
                "State Change" => new SolidColorBrush(Color.FromRgb(46, 68, 89)),
                "New" => new SolidColorBrush(Color.FromRgb(18, 74, 110)),
                _ => new SolidColorBrush(Color.FromRgb(40, 40, 40))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public class GooseEventFgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value?.ToString() ?? "";

            return text switch
            {
                "State Change" => new SolidColorBrush(Color.FromRgb(135, 210, 255)),
                "New" => new SolidColorBrush(Color.FromRgb(170, 226, 255)),
                _ => Brushes.White
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;

            return false;
        }
    }
}