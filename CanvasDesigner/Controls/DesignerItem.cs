using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CanvasDesigner.Converters;
using CanvasDesigner.Models;

namespace CanvasDesigner.Controls
{
    /// <summary>
    /// 画布上的图元宿主（相当于 LinearUI 的 Chamber/SensorBtn 在画面中的容器）。
    /// 职责：
    ///  1. 依据图元数据用 DrawingContext 自绘外观（矩形/椭圆/文本/按钮/指示灯/图片/线段）；
    ///  2. 点击选中、拖动移动、八向拖拽缩放（带最小尺寸与可选网格吸附）；
    ///  3. 选中时绘制蓝色虚线选框 + 8 个缩放手柄。
    /// </summary>
    public class DesignerItem : FrameworkElement
    {
        public const double MinSize = 8;
        private const double HandleSize = 8;

        private readonly ComponentBase _component;
        private bool _dragging;
        private string _resizeHandle;   // N/S/E/W/NW/NE/SW/SE
        private Point _startPoint;      // 画布坐标（文档坐标）
        private Point _startPos;
        private Point _startSize;
        private bool _startMove;

        public ComponentBase Component => _component;

        public event EventHandler RequestSelect;

        /// <summary>网格吸附函数 + 开关（由画布所有者注入）</summary>
        public Func<double, double> SnapFn { get; set; }
        public bool SnapEnabled { get; set; }

        public DesignerItem(ComponentBase component)
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
            SnapsToDevicePixels = true;
            Focusable = true;
            Cursor = Cursors.SizeAll;
            _component.PropertyChanged += OnComponentPropertyChanged;
        }

        // ---------- 尺寸由数据驱动 ----------
        protected override Size MeasureOverride(Size availableSize)
            => new Size(_component.Width, _component.Height);

        private void OnComponentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 位置/层级改变时同步到 Canvas 附加属性
            if (e.PropertyName == nameof(ComponentBase.X) || e.PropertyName == nameof(ComponentBase.Y))
            {
                Canvas.SetLeft(this, _component.X);
                Canvas.SetTop(this, _component.Y);
            }
            else if (e.PropertyName == nameof(ComponentBase.ZIndex))
            {
                Panel.SetZIndex(this, _component.ZIndex);
            }
            else if (e.PropertyName == nameof(ComponentBase.IsSelected))
            {
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(ComponentBase.Width) ||
                     e.PropertyName == nameof(ComponentBase.Height))
            {
                InvalidateMeasure();
                InvalidateVisual();
            }
            else
            {
                InvalidateVisual();
            }
        }

        // ---------- 命中测试与交互 ----------
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            // 自绘元素默认命中区域仅为可见内容，这里让整个布局矩形都可命中，
            // 保证点选/拖拽/边缘缩放都有效。
            var pt = hitTestParameters.HitPoint;
            return (pt.X >= 0 && pt.Y >= 0 && pt.X <= RenderSize.Width && pt.Y <= RenderSize.Height)
                ? new PointHitTestResult(this, pt)
                : null;
        }

        private string HitHandle(Point p)
        {
            double w = _component.Width, h = _component.Height;
            double r = HandleSize / 2;
            bool nearLeft = p.X <= r, nearRight = p.X >= w - r;
            bool nearTop = p.Y <= r, nearBottom = p.Y >= h - r;
            if (nearLeft && nearTop) return "NW";
            if (nearRight && nearTop) return "NE";
            if (nearLeft && nearBottom) return "SW";
            if (nearRight && nearBottom) return "SE";
            if (nearTop) return "N";
            if (nearBottom) return "S";
            if (nearLeft) return "W";
            if (nearRight) return "E";
            return null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            RequestSelect?.Invoke(this, EventArgs.Empty);
            _component.IsSelected = true;

            var p = e.GetPosition(this);
            _resizeHandle = HitHandle(p);
            _dragging = true;
            _startMove = _resizeHandle == null;
            _startPoint = ToCanvasPoint(e);
            _startPos = new Point(_component.X, _component.Y);
            _startSize = new Point(_component.Width, _component.Height);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_dragging)
            {
                var p = e.GetPosition(this);
                var h = HitHandle(p);
                Cursor = h switch
                {
                    "N" or "S" => Cursors.SizeNS,
                    "E" or "W" => Cursors.SizeWE,
                    "NW" or "SE" => Cursors.SizeNWSE,
                    "NE" or "SW" => Cursors.SizeNESW,
                    _ => Cursors.SizeAll,
                };
                return;
            }

            var cur = ToCanvasPoint(e);
            double dx = cur.X - _startPoint.X;
            double dy = cur.Y - _startPoint.Y;

            if (_startMove)
            {
                double nx = SnapValue(_startPos.X + dx);
                double ny = SnapValue(_startPos.Y + dy);
                _component.X = Math.Max(0, nx);
                _component.Y = Math.Max(0, ny);
            }
            else
            {
                ResizeTo(dx, dy);
            }
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        private Point ToCanvasPoint(MouseEventArgs e)
        {
            var canvas = GetCanvas();
            return canvas != null ? e.GetPosition(canvas) : e.GetPosition(this);
        }

        private Canvas GetCanvas() => this.Parent as Canvas;

        private double SnapValue(double v)
            => SnapEnabled && SnapFn != null ? SnapFn(v) : v;

        private void ResizeTo(double dx, double dy)
        {
            double w = _component.Width, h = _component.Height;
            double x = _component.X, y = _component.Y;

            switch (_resizeHandle)
            {
                case "E": w = _startSize.X + dx; break;
                case "S": h = _startSize.Y + dy; break;
                case "W": w = _startSize.X - dx; x = _startPos.X + dx; break;
                case "N": h = _startSize.Y - dy; y = _startPos.Y + dy; break;
                case "SE": w = _startSize.X + dx; h = _startSize.Y + dy; break;
                case "NW": w = _startSize.X - dx; h = _startSize.Y - dy; x = _startPos.X + dx; y = _startPos.Y + dy; break;
                case "NE": w = _startSize.X + dx; h = _startSize.Y - dy; y = _startPos.Y + dy; break;
                case "SW": w = _startSize.X - dx; h = _startSize.Y + dy; x = _startPos.X + dx; break;
            }

            // 最小尺寸约束（对左侧/上侧手柄需回推 x/y）
            if (w < MinSize)
            {
                if (_resizeHandle.Contains("W")) x = _startPos.X + (_startSize.X - MinSize);
                w = MinSize;
            }
            if (h < MinSize)
            {
                if (_resizeHandle.Contains("N")) y = _startPos.Y + (_startSize.Y - MinSize);
                h = MinSize;
            }

            // 吸附后尺寸可能变化，左侧/上侧手柄再回推，保证右下角不动
            double sw = SnapValue(w), sh = SnapValue(h);
            if (_resizeHandle.Contains("W"))
            {
                x = _startPos.X + (_startSize.X - sw);
                _component.X = Math.Max(0, x);
            }
            if (_resizeHandle.Contains("N"))
            {
                y = _startPos.Y + (_startSize.Y - sh);
                _component.Y = Math.Max(0, y);
            }
            _component.Width = Math.Max(MinSize, sw);
            _component.Height = Math.Max(MinSize, sh);
        }

        // ---------- 绘制 ----------
        protected override void OnRender(DrawingContext dc)
        {
            double w = _component.Width, h = _component.Height;
            if (w <= 0 || h <= 0) return;

            DrawContent(dc, w, h);

            if (_component.IsSelected)
            {
                // 蓝色虚线选框
                var pen = new Pen(Brushes.DodgerBlue, 1)
                {
                    DashStyle = new DashStyle(new double[] { 3, 2 }, 0)
                };
                dc.DrawRectangle(null, pen, new Rect(0.5, 0.5, Math.Max(0, w - 1), Math.Max(0, h - 1)));

                // 8 个缩放手柄
                var handleBrush = Brushes.White;
                var handlePen = new Pen(Brushes.DodgerBlue, 1);
                double hs = HandleSize;
                double half = hs / 2;
                AddHandle(dc, handleBrush, handlePen, 0, 0, hs);
                AddHandle(dc, handleBrush, handlePen, w / 2 - half, 0, hs);
                AddHandle(dc, handleBrush, handlePen, w - hs, 0, hs);
                AddHandle(dc, handleBrush, handlePen, 0, h / 2 - half, hs);
                AddHandle(dc, handleBrush, handlePen, w - hs, h / 2 - half, hs);
                AddHandle(dc, handleBrush, handlePen, 0, h - hs, hs);
                AddHandle(dc, handleBrush, handlePen, w / 2 - half, h - hs, hs);
                AddHandle(dc, handleBrush, handlePen, w - hs, h - hs, hs);
            }
        }

        private static void AddHandle(DrawingContext dc, Brush fill, Pen pen, double x, double y, double s)
            => dc.DrawRectangle(fill, pen, new Rect(x, y, s, s));

        private void DrawContent(DrawingContext dc, double w, double h)
        {
            switch (_component)
            {
                case RectangleComponent r: DrawRectanglePart(dc, r, w, h); break;
                case EllipseComponent e: DrawEllipsePart(dc, e, w, h); break;
                case TextComponent t: DrawTextPart(dc, t, w, h); break;
                case ButtonComponent b: DrawButtonPart(dc, b, w, h); break;
                case LampComponent l: DrawLampPart(dc, l, w, h); break;
                case ImageComponent img: DrawImagePart(dc, img, w, h); break;
                case LineComponent line: DrawLinePart(dc, line, w, h); break;
                default:
                    dc.DrawRectangle(Brushes.LightGray, new Pen(Brushes.Gray, 1), new Rect(0, 0, w, h));
                    break;
            }
        }

        private static Pen MakePen(string color, double thickness)
            => new Pen(BrushUtil.ToBrush(color), Math.Max(0.5, thickness));

        private static void DrawRectanglePart(DrawingContext dc, RectangleComponent r, double w, double h)
        {
            var radius = Math.Min(Math.Max(0, r.CornerRadius), Math.Min(w, h) / 2);
            var geo = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
            dc.DrawGeometry(BrushUtil.ToBrush(r.Fill), MakePen(r.Stroke, r.StrokeThickness), geo);
        }

        private static void DrawEllipsePart(DrawingContext dc, EllipseComponent e, double w, double h)
        {
            var geo = new EllipseGeometry(new Rect(0, 0, w, h));
            dc.DrawGeometry(BrushUtil.ToBrush(e.Fill), MakePen(e.Stroke, e.StrokeThickness), geo);
        }

        private void DrawTextPart(DrawingContext dc, TextComponent t, double w, double h)
        {
            if (string.IsNullOrEmpty(t.Text)) return;
            var typeface = new Typeface(new FontFamily(t.FontFamily),
                FontStyles.Normal, t.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            var ft = new FormattedText(t.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                typeface, t.FontSize, BrushUtil.ToBrush(t.Foreground), VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(0, w),
                MaxTextHeight = Math.Max(0, h),
                Trimming = TextTrimming.CharacterEllipsis,
            };

            double x = t.HAlign switch
            {
                "Left" => 0,
                "Right" => Math.Max(0, w - ft.Width),
                _ => Math.Max(0, (w - ft.Width) / 2),
            };
            double y = t.VAlign switch
            {
                "Top" => 0,
                "Bottom" => Math.Max(0, h - ft.Height),
                _ => Math.Max(0, (h - ft.Height) / 2),
            };
            dc.DrawText(ft, new Point(x, y));
        }

        private void DrawButtonPart(DrawingContext dc, ButtonComponent b, double w, double h)
        {
            var radius = Math.Min(Math.Max(0, b.CornerRadius), Math.Min(w, h) / 2);
            var geo = new RectangleGeometry(new Rect(0.5, 0.5, Math.Max(0, w - 1), Math.Max(0, h - 1)), radius, radius);
            dc.DrawGeometry(BrushUtil.ToBrush(b.Background), MakePen(b.Border, 1), geo);

            if (string.IsNullOrEmpty(b.Text)) return;
            var typeface = new Typeface(new FontFamily(b.FontFamily), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var ft = new FormattedText(b.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                typeface, b.FontSize, BrushUtil.ToBrush(b.Foreground), VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(0, w - 8),
                Trimming = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(ft, new Point(Math.Max(0, (w - ft.Width) / 2), Math.Max(0, (h - ft.Height) / 2)));
        }

        private void DrawLampPart(DrawingContext dc, LampComponent l, double w, double h)
        {
            double d = Math.Min(w, h);
            double labelSpace = string.IsNullOrEmpty(l.Label) ? 0 : Math.Min(14, Math.Max(8, d * 0.22));
            double cd = d - labelSpace - 2;
            if (cd < 4) cd = Math.Max(4, d - 2);
            var center = new Point(w / 2, Math.Max(cd / 2, (h - labelSpace) / 2));

            var ring = new Pen(BrushUtil.ToBrush(l.BorderColor), Math.Max(1, cd * 0.08));
            dc.DrawEllipse(Brushes.Transparent, ring, center, cd / 2, cd / 2);
            double inner = cd * 0.72;
            dc.DrawEllipse(BrushUtil.ToBrush(l.IsOn ? l.OnColor : l.OffColor), null, center, inner / 2, inner / 2);

            if (!string.IsNullOrEmpty(l.Label))
            {
                var typeface = new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var ft = new FormattedText(l.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    typeface, Math.Max(8, d * 0.16), Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = Math.Max(0, w),
                    Trimming = TextTrimming.CharacterEllipsis,
                };
                dc.DrawText(ft, new Point(Math.Max(0, (w - ft.Width) / 2), Math.Max(0, h - ft.Height - 1)));
            }
        }

        private void DrawImagePart(DrawingContext dc, ImageComponent img, double w, double h)
        {
            var bitmap = ImageCache.Get(img.SourcePath);
            if (bitmap == null)
            {
                var pen = new Pen(Brushes.Gray, 1) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
                dc.DrawRectangle(Brushes.Transparent, pen, new Rect(0.5, 0.5, Math.Max(0, w - 1), Math.Max(0, h - 1)));
                if (!string.IsNullOrEmpty(img.SourcePath))
                    dc.DrawText(new FormattedText("图片不存在", CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                        12, Brushes.Gray,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(4, 4));
                return;
            }
            var target = ImageCache.ComputeTarget(bitmap, w, h, img.Stretch);
            dc.DrawImage(bitmap, target);
        }

        private void DrawLinePart(DrawingContext dc, LineComponent line, double w, double h)
        {
            var pen = new Pen(BrushUtil.ToBrush(line.Stroke), line.StrokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            Point p0, p1;
            switch (line.Direction)
            {
                case "Vertical":
                    p0 = new Point(w / 2, 0); p1 = new Point(w / 2, h); break;
                case "Diagonal":
                    p0 = new Point(0, 0); p1 = new Point(w, h); break;
                default:
                    p0 = new Point(0, h / 2); p1 = new Point(w, h / 2); break;
            }
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(p0, false, false);
                ctx.LineTo(p1, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
        }
    }

    /// <summary>图片缓存（避免重复解码）</summary>
    public static class ImageCache
    {
        private static readonly System.Collections.Generic.Dictionary<string, ImageSource> _cache = new();
        private static string _lastPath;
        private static ImageSource _last;

        public static ImageSource Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
            if (_lastPath == path) return _last;
            if (_cache.TryGetValue(path, out var src)) { _lastPath = path; _last = src; return src; }
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                if (_cache.Count > 200) _cache.Clear();
                _cache[path] = bmp;
                _lastPath = path; _last = bmp;
                return bmp;
            }
            catch { return null; }
        }

        public static Rect ComputeTarget(ImageSource src, double w, double h, string stretch)
        {
            double iw = src.Width, ih = src.Height;
            if (iw <= 0 || ih <= 0) return new Rect(0, 0, w, h);
            double scaleX = w / iw, scaleY = h / ih;
            switch (stretch)
            {
                case "None":
                    return new Rect(0, 0, Math.Min(w, iw), Math.Min(h, ih));
                case "Fill":
                    return new Rect(0, 0, w, h);
                case "UniformToFill":
                {
                    double s = Math.Max(scaleX, scaleY);
                    double dw2 = iw * s, dh2 = ih * s;
                    return new Rect((w - dw2) / 2, (h - dh2) / 2, dw2, dh2);
                }
                default: // Uniform
                {
                    double s = Math.Min(scaleX, scaleY);
                    double dw2 = iw * s, dh2 = ih * s;
                    return new Rect((w - dw2) / 2, (h - dh2) / 2, dw2, dh2);
                }
            }
        }
    }
}
