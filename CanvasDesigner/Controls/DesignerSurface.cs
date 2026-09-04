using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanvasDesigner.Converters;

namespace CanvasDesigner.Controls
{
    /// <summary>
    /// 设计画布表面：绘制背景色与可选网格，图元作为其子元素叠加其上。
    /// 相当于 Linear 各 Overview/Normal 面板背后的底布。
    /// </summary>
    public class DesignerSurface : Canvas
    {
        private string _backgroundColor = "#FF263238";
        private double _gridSize = 20;
        private bool _showGrid = true;

        public DesignerSurface()
        {
            SnapsToDevicePixels = true;
        }

        public void SetLook(string backgroundColor, double gridSize, bool showGrid)
        {
            _backgroundColor = string.IsNullOrWhiteSpace(backgroundColor) ? "#FF263238" : backgroundColor;
            _gridSize = gridSize >= 4 ? gridSize : 20;
            _showGrid = showGrid;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 0 : Width);
            double h = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 0 : Height);
            if (w <= 0 || h <= 0) return;

            // 背景
            dc.DrawRectangle(BrushUtil.ToBrush(_backgroundColor), null, new Rect(0, 0, w, h));

            // 网格
            if (_showGrid && _gridSize >= 4)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), 0.6);
                for (double x = _gridSize; x < w; x += _gridSize)
                    dc.DrawLine(pen, new Point(x, 0), new Point(x, h));
                for (double y = _gridSize; y < h; y += _gridSize)
                    dc.DrawLine(pen, new Point(0, y), new Point(w, y));
            }
        }
    }
}
