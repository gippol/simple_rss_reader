using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace RssReader.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool boolVal = false;
        if (value is bool b)
        {
            boolVal = b;
        }
        else if (value is int count)
        {
            boolVal = count > 0;
        }
        else if (value != null)
        {
            boolVal = true;
        }

        if (Invert)
        {
            boolVal = !boolVal;
        }

        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            var res = v == Visibility.Visible;
            return Invert ? !res : res;
        }
        return false;
    }
}
