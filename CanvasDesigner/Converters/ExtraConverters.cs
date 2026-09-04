using System.Windows;
using System.Windows.Data;

namespace CanvasDesigner.Converters
{
    /// <summary>bool → Visibility（反向）</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>常用颜色候选（供颜色编辑器下拉）</summary>
    public static class Palette
    {
        public static readonly string[] Colors =
        {
            "#FF000000", "#FFFFFFFF", "#FFE53935", "#FFFF6F00", "#FFFFEB3B", "#FF43A047",
            "#FF00897B", "#FF00ACC1", "#FF1E88E5", "#FF3949AB", "#FF8E24AA", "#FFF4511E",
            "#FF90A4AE", "#FF607D8B", "#FF795548", "#FF37474F", "#FF263238", "#FFFFC107",
            "#FF3F7FBF", "#FF1F4E79", "#FF00E676", "#FF6E6E6E", "#FFB0BEC5", "#FFDCE6F5",
            "#FF5B9BD5", "#FF2ECC71", "#FFCC0000", "#FF8C9EFF", "#FFFFD180", "#FFA5D6A7",
        };
    }
}
