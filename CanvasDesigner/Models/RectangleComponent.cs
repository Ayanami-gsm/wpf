namespace CanvasDesigner.Models
{
    /// <summary>矩形（支持圆角），对应 LinearUI 里的设备框/槽等占位块</summary>
    public class RectangleComponent : ComponentBase
    {
        private string _fill = "#FF5B9BD5";
        private string _stroke = "#FF2B3A4A";
        private double _strokeThickness = 1.5;
        private double _cornerRadius = 4;

        public RectangleComponent() { Width = 160; Height = 100; }

        [EditorProp("填充色", EditorKind.Color, "外观", 0)]
        public string Fill { get => _fill; set => Set(ref _fill, value); }

        [EditorProp("边框色", EditorKind.Color, "外观", 1)]
        public string Stroke { get => _stroke; set => Set(ref _stroke, value); }

        [EditorProp("边框粗细", EditorKind.Number, "外观", 2, Min = 0, Max = 20)]
        public double StrokeThickness { get => _strokeThickness; set => Set(ref _strokeThickness, Math.Max(0, value)); }

        [EditorProp("圆角半径", EditorKind.Number, "外观", 3, Min = 0, Max = 80)]
        public double CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, Math.Max(0, value)); }
    }
}
