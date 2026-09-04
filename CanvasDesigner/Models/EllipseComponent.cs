namespace CanvasDesigner.Models
{
    /// <summary>椭圆/圆</summary>
    public class EllipseComponent : ComponentBase
    {
        private string _fill = "#FF81C784";
        private string _stroke = "#FF2B3A4A";
        private double _strokeThickness = 1.5;

        public EllipseComponent() { Width = 160; Height = 100; }

        [EditorProp("填充色", EditorKind.Color, "外观", 0)]
        public string Fill { get => _fill; set => Set(ref _fill, value); }

        [EditorProp("边框色", EditorKind.Color, "外观", 1)]
        public string Stroke { get => _stroke; set => Set(ref _stroke, value); }

        [EditorProp("边框粗细", EditorKind.Number, "外观", 2, Min = 0, Max = 20)]
        public double StrokeThickness { get => _strokeThickness; set => Set(ref _strokeThickness, Math.Max(0, value)); }
    }
}
