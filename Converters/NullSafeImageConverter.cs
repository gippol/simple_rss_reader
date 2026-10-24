using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace RssReader.Converters;

/// <summary>
/// URL文字列を安全にBitmapImageへ変換するコンバーター。
/// nullや空文字、不正なURLの場合はnullを返し、XamlBindingHelperの例外を防ぎます。
/// </summary>
public class NullSafeImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            try
            {
                return new BitmapImage(uri);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        throw new NotImplementedException();
    }
}
