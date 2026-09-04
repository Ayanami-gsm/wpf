using System;
using System.Collections.Generic;
using System.Linq;

namespace CanvasDesigner.Models
{
    /// <summary>工具箱条目：类型 + 分组 + 默认尺寸</summary>
    public class ComponentCatalogEntry
    {
        public Type ComponentType { get; }
        public string DisplayName { get; }
        public string Group { get; }
        public double DefaultWidth { get; }
        public double DefaultHeight { get; }
        public string IconChar { get; }

        public ComponentCatalogEntry(Type componentType, string displayName, string group,
            double defaultWidth, double defaultHeight, string iconChar)
        {
            ComponentType = componentType;
            DisplayName = displayName;
            Group = group;
            DefaultWidth = defaultWidth;
            DefaultHeight = defaultHeight;
            IconChar = iconChar;
        }

        public ComponentBase CreateInstance()
        {
            var c = (ComponentBase)Activator.CreateInstance(ComponentType);
            c.Width = DefaultWidth;
            c.Height = DefaultHeight;
            return c;
        }
    }

    /// <summary>
    /// 组件注册表：工具箱显示 + JSON 类型分发共用。
    /// 相当于 Linear 工程里"每个部件一个类 + 由 Type 字符串映射"的注册机制。
    /// </summary>
    public static class ComponentCatalog
    {
        private static readonly List<ComponentCatalogEntry> _entries = new()
        {
            new ComponentCatalogEntry(typeof(RectangleComponent), "矩形", "基本形状", 160, 100, "▭"),
            new ComponentCatalogEntry(typeof(EllipseComponent),   "椭圆", "基本形状", 160, 100, "●"),
            new ComponentCatalogEntry(typeof(LineComponent),      "线段", "基本形状", 160, 4, "╱"),
            new ComponentCatalogEntry(typeof(TextComponent),      "文本", "显示", 200, 40, "T"),
            new ComponentCatalogEntry(typeof(ButtonComponent),    "按钮", "控件", 120, 40, "▣"),
            new ComponentCatalogEntry(typeof(LampComponent),      "指示灯", "显示", 70, 70, "◉"),
            new ComponentCatalogEntry(typeof(ImageComponent),     "图片", "显示", 160, 120, "▧"),
        };

        private static readonly Dictionary<string, ComponentCatalogEntry> _byKey =
            _entries.ToDictionary(e => e.ComponentType.Name, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<Type, ComponentCatalogEntry> _byType =
            _entries.ToDictionary(e => e.ComponentType);

        public static IReadOnlyList<ComponentCatalogEntry> All => _entries;

        public static ComponentCatalogEntry GetByType(Type t)
            => _byType.TryGetValue(t, out var e) ? e : null;

        /// <summary>按类型名（TypeName 字符串）取注册项，用于 JSON 反序列化</summary>
        public static ComponentCatalogEntry GetByKey(string typeName)
            => _byKey.TryGetValue(typeName, out var e) ? e : null;

        public static IEnumerable<string> Groups
            => _entries.Select(x => x.Group).Distinct();
    }
}
