using System.Windows;
using System.Windows.Controls;
using CanvasDesigner.ViewModels;

namespace CanvasDesigner.Controls
{
    /// <summary>
    /// 属性编辑器模板选择器：根据 PropertyRowViewModel.Kind 在资源中查找
    /// "Editor_文本/数值/开关/颜色/选项/字体/文件" 模板。
    /// </summary>
    public class EditorTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is not PropertyRowViewModel row) return null;
            if (container is not FrameworkElement fe) return null;

            string key = "Editor_" + row.Kind.ToString();
            return fe.TryFindResource(key) as DataTemplate;
        }
    }
}
