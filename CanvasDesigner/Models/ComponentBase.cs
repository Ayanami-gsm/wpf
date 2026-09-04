using System.Text.Json.Serialization;

namespace CanvasDesigner.Models
{
    /// <summary>
    /// 画布上的图元基类（借鉴 LinearUI OverviewDeviceItem / CurDeviceClickItem：
    /// 部件 = 一个可序列化的纯数据对象，几何/外观都作为属性暴露）。
    /// </summary>
    public abstract class ComponentBase : ObservableObject
    {
        private string _name = "组件";
        private double _x, _y, _width = 120, _height = 80;
        private int _zIndex;
        private bool _isSelected;

        [JsonIgnore] public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

        [JsonIgnore]
        public string TypeLabel => CatalogEntry?.DisplayName ?? "组件";

        [JsonIgnore]
        public ComponentCatalogEntry CatalogEntry => ComponentCatalog.GetByType(GetType());

        [EditorProp("名称", EditorKind.Text, "属性", 0)]
        public string Name { get => _name; set => Set(ref _name, value); }

        [EditorProp("X", EditorKind.Number, "布局", 1)]
        public double X { get => _x; set => Set(ref _x, value); }

        [EditorProp("Y", EditorKind.Number, "布局", 2)]
        public double Y { get => _y; set => Set(ref _y, value); }

        [EditorProp("宽度", EditorKind.Number, "布局", 3, Min = 1)]
        public double Width { get => _width; set => Set(ref _width, Math.Max(1, value)); }

        [EditorProp("高度", EditorKind.Number, "布局", 4, Min = 1)]
        public double Height { get => _height; set => Set(ref _height, Math.Max(1, value)); }

        [EditorProp("Z 序", EditorKind.Number, "布局", 5, Min = 0)]
        public int ZIndex { get => _zIndex; set => Set(ref _zIndex, Math.Max(0, value)); }

        /// <summary>深拷贝一份（用于复制命令）</summary>
        public ComponentBase Clone()
        {
            var json = Services.DocumentSerializer.SerializeComponent(this);
            return Services.DocumentSerializer.DeserializeComponent(json);
        }
    }
}
