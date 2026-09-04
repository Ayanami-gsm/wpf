using System;

namespace CanvasDesigner.Models
{
    /// <summary>属性面板编辑器类型</summary>
    public enum EditorKind
    {
        Text,          // 单行文本
        MultiText,     // 多行文本
        Number,        // 数值
        Bool,          // 开关
        Color,         // 颜色（hex 字符串）
        Options,       // 下拉选项（字符串值）
        Font,          // 字体
        File,          // 文件路径
    }

    /// <summary>
    /// 标注哪些公开属性需要出现在右侧属性面板中。
    /// 借鉴 LinearUI 各部件属性 + 配置项的思路：用元数据驱动属性面板。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class EditorPropAttribute : Attribute
    {
        public string Label { get; }
        public EditorKind Kind { get; }
        public string Category { get; }
        public int Order { get; }

        /// <summary>Options 编辑器的候选项</summary>
        public string[] Options { get; set; }

        /// <summary>Number 下限</summary>
        public double Min { get; set; } = double.NaN;

        /// <summary>Number 上限</summary>
        public double Max { get; set; } = double.NaN;

        public EditorPropAttribute(string label, EditorKind kind = EditorKind.Text,
            string category = "属性", int order = 100)
        {
            Label = label;
            Kind = kind;
            Category = category;
            Order = order;
        }
    }
}
