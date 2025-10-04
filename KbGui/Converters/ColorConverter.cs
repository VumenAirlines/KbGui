using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KbGui.Converters;

public class ColorConverter_cs
{
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUserInput)
            {
                return isUserInput ? new SolidColorBrush(Color.Parse("#00FF00")) 
                    : new SolidColorBrush(Color.Parse("#CCCCCC"));
            }
            return new SolidColorBrush(Color.Parse("#CCCCCC"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}