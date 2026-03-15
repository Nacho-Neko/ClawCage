using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ClawCage.WinUI.Converters
{
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var boolValue = value is true;
            var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
            if (invert) boolValue = !boolValue;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
