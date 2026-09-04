namespace CanvasDesigner.Models
{
    /// <summary>按钮（运行时无实际点击逻辑，仅画面呈现，如 LinearUI SensorBtn 的静态外观）</summary>
    public class ButtonComponent : ComponentBase
    {
        private string _text = "按钮";
        private string _fontFamily = "Microsoft YaHei";
        private double _fontSize = 14;
        private string _foreground = "#FFFFFFFF";
        private string _background = "#FF3F7FBF";
        private string _border = "#FF1F4E79";
        private double _cornerRadius = 4;

        public ButtonComponent() { Width = 120; Height = 40; }

        [EditorProp("文本", EditorKind.Text, "内容", 0)]
        public string Text { get => _text; set => Set(ref _text, value); }

        [EditorProp("字体", EditorKind.Font, "外观", 1)]
        public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }

        [EditorProp("字号", EditorKind.Number, "外观", 2, Min = 6, Max = 200)]
        public double FontSize { get => _fontSize; set => Set(ref _fontSize, Math.Max(1, value)); }

        [EditorProp("文字颜色", EditorKind.Color, "外观", 3)]
        public string Foreground { get => _foreground; set => Set(ref _foreground, value); }

        [EditorProp("背景色", EditorKind.Color, "外观", 4)]
        public string Background { get => _background; set => Set(ref _background, value); }

        [EditorProp("边框色", EditorKind.Color, "外观", 5)]
        public string Border { get => _border; set => Set(ref _border, value); }

        [EditorProp("圆角半径", EditorKind.Number, "外观", 6, Min = 0, Max = 40)]
        public double CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, Math.Max(0, value)); }
    }
}
