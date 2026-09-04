namespace CanvasDesigner.Models
{
    /// <summary>指示灯（借鉴 LinearUI SensorBtn / SwitchButton：On/Off 两色切换）</summary>
    public class LampComponent : ComponentBase
    {
        private string _onColor = "#FF00E676";
        private string _offColor = "#FF6E6E6E";
        private string _borderColor = "#FFB0BEC5";
        private bool _isOn = true;
        private string _label = "";

        public LampComponent() { Width = 70; Height = 70; }

        [EditorProp("亮灯颜色", EditorKind.Color, "外观", 0)]
        public string OnColor { get => _onColor; set => Set(ref _onColor, value); }

        [EditorProp("灭灯颜色", EditorKind.Color, "外观", 1)]
        public string OffColor { get => _offColor; set => Set(ref _offColor, value); }

        [EditorProp("外圈颜色", EditorKind.Color, "外观", 2)]
        public string BorderColor { get => _borderColor; set => Set(ref _borderColor, value); }

        [EditorProp("点亮", EditorKind.Bool, "外观", 3)]
        public bool IsOn { get => _isOn; set => Set(ref _isOn, value); }

        [EditorProp("标签", EditorKind.Text, "内容", 4)]
        public string Label { get => _label; set => Set(ref _label, value); }
    }
}
