namespace CanvasDesigner.Models
{
    /// <summary>文本（类似 OverViewTextBlock）</summary>
    public class TextComponent : ComponentBase
    {
        private string _text = "文本";
        private string _fontFamily = "Microsoft YaHei";
        private double _fontSize = 16;
        private string _foreground = "#FFFFFFFF";
        private bool _bold;
        private string _halign = "Center";   // Left / Center / Right
        private string _valign = "Middle";   // Top / Middle / Bottom

        public TextComponent() { Width = 200; Height = 40; }

        [EditorProp("文本", EditorKind.MultiText, "内容", 0)]
        public string Text { get => _text; set => Set(ref _text, value); }

        [EditorProp("字体", EditorKind.Font, "外观", 1)]
        public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }

        [EditorProp("字号", EditorKind.Number, "外观", 2, Min = 6, Max = 200)]
        public double FontSize { get => _fontSize; set => Set(ref _fontSize, Math.Max(1, value)); }

        [EditorProp("字体颜色", EditorKind.Color, "外观", 3)]
        public string Foreground { get => _foreground; set => Set(ref _foreground, value); }

        [EditorProp("加粗", EditorKind.Bool, "外观", 4)]
        public bool Bold { get => _bold; set => Set(ref _bold, value); }

        [EditorProp("水平对齐", EditorKind.Options, "外观", 5,
            Options = new[] { "Left", "Center", "Right" })]
        public string HAlign { get => _halign; set => Set(ref _halign, value); }

        [EditorProp("垂直对齐", EditorKind.Options, "外观", 6,
            Options = new[] { "Top", "Middle", "Bottom" })]
        public string VAlign { get => _valign; set => Set(ref _valign, value); }
    }
}
