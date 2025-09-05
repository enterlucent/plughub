using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;

namespace PlugHub.Converters
{
    public class UriToBitmapConverter : IValueConverter
    {
        public static readonly UriToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string uriString && !string.IsNullOrWhiteSpace(uriString))
            {
                try
                {
                    Uri uri = new(uriString, UriKind.RelativeOrAbsolute);

                    if (uri.IsAbsoluteUri)
                    {
                        System.IO.Stream stream = AssetLoader.Open(uri);

                        if (stream != null)
                            return new Bitmap(stream);
                    }
                }
                catch { }
            }

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}