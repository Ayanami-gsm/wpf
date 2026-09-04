using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CanvasDesigner.Converters
{
    /// <summary>颜色字符串(#AARRGGBB / #RRGGBB / 名称) → Brush</summary>
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => BrushUtil.ToBrush(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>字符串 → FontFamily</summary>
    public class StringToFontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            try { return string.IsNullOrWhiteSpace(s) ? new FontFamily("Microsoft YaHei") : new FontFamily(s); }
            catch { return new FontFamily("Microsoft YaHei"); }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>"Left/Center/Right" → TextAlignment</summary>
    public class TextAlignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string) switch
            {
                "Left" => TextAlignment.Left,
                "Right" => TextAlignment.Right,
                _ => TextAlignment.Center,
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>"Top/Middle/Bottom" → VerticalAlignment</summary>
    public class VerticalAlignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string) switch
            {
                "Top" => VerticalAlignment.Top,
                "Bottom" => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Center,
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>"None/Fill/Uniform/UniformToFill" → Stretch</summary>
    public class StretchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string) switch
            {
                "None" => Stretch.None,
                "Fill" => Stretch.Fill,
                "UniformToFill" => Stretch.UniformToFill,
                _ => Stretch.Uniform,
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>路径字符串 → ImageSource（文件不存在返回 null）</summary>
    public class PathToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                var uri = new Uri(path, UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>Lamp 组合值 [IsOn, OnColor, OffColor] → 填充色</summary>
    public class LampBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool on = values.Length > 0 && values[0] is true;
            var onColor = values.Length > 1 ? values[1] as string : null;
            var offColor = values.Length > 2 ? values[2] as string : null;
            return BrushUtil.ToBrush(on ? onColor : offColor);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => null;
    }

    /// <summary>帮助类：解析颜色字符串</summary>
    public static class BrushUtil
    {
        public static Brush ToBrush(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
                var conv = new BrushConverter();
                var b = conv.ConvertFromString(hex);
                if (b is Brush brush) { brush.Freeze(); return brush; }
                return Brushes.Transparent;
            }
            catch { return Brushes.Transparent; }
        }
    }

    /// <summary>int/string 是否等于参数 → 可见性（用 BoolToVis 亦可）</summary>
    public class IsEqualToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
