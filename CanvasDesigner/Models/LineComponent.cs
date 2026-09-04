namespace CanvasDesigner.Models
{
    /// <summary>直线/线段（借鉴 LinearUI 的管路/连线，简化为通用线段）</summary>
    public class LineComponent : ComponentBase
    {
        private string _stroke = "#FF90A4AE";
        private double _strokeThickness = 3;
        private string _direction = "Horizontal";  // Horizontal / Vertical / Diagonal

        public LineComponent() { Width = 160; Height = 4; }

        [EditorProp("颜色", EditorKind.Color, "外观", 0)]
        public string Stroke { get => _stroke; set => Set(ref _stroke, value); }

        [EditorProp("粗细", EditorKind.Number, "外观", 1, Min = 1, Max = 30)]
        public double StrokeThickness { get => _strokeThickness; set => Set(ref _strokeThickness, Math.Max(1, value)); }

        [EditorProp("方向", EditorKind.Options, "外观", 2,
            Options = new[] { "Horizontal", "Vertical", "Diagonal" })]
        public string Direction { get => _direction; set => Set(ref _direction, value); }
    }
}
