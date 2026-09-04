using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using CanvasDesigner.Models;
using CanvasDesigner.Services;

namespace CanvasDesigner.ViewModels
{
    /// <summary>属性面板的一行（一个属性编辑器），双向读写组件属性</summary>
    public class PropertyRowViewModel : ObservableObject
    {
        private readonly ComponentBase _component;
        private readonly PropertyMeta _meta;
        private string _textValue;
        private bool _boolValue;
        private string _optionValue;

        public string Label => _meta.Attr.Label;
        public EditorKind Kind => _meta.Attr.Kind;
        public string Category => _meta.Attr.Category;
        public string PropertyName => _meta.Name;

        public string[] Options => _meta.Attr.Options ?? Array.Empty<string>();
        public double Min => _meta.Attr.Min;
        public double Max => _meta.Attr.Max;

        public ICommand BrowseCommand { get; }

        public PropertyRowViewModel(ComponentBase component, PropertyMeta meta)
        {
            _component = component;
            _meta = meta;
            BrowseCommand = new RelayCommand(_ => BrowseFile());

            _textValue = FormatText(_meta.GetValue(component));
            if (Kind == EditorKind.Bool) _boolValue = (bool)_meta.GetValue(component);
            if (Kind == EditorKind.Options) _optionValue = _meta.GetValue(component)?.ToString();
        }

        /// <summary>外部（如画布拖拽）导致属性变化时刷新显示</summary>
        public void RefreshFromComponent()
        {
            if (Kind == EditorKind.Bool)
            {
                BoolValue = (bool)_meta.GetValue(_component);
                return;
            }
            if (Kind == EditorKind.Options)
            {
                OptionValue = _meta.GetValue(_component)?.ToString();
                return;
            }
            TextValue = FormatText(_meta.GetValue(_component));
        }

        private string FormatText(object v) => v switch
        {
            double d => d.ToString("0.####", CultureInfo.InvariantCulture),
            _ => v?.ToString() ?? "",
        };

        public string TextValue
        {
            get => _textValue;
            set
            {
                if (_textValue == value) return;
                if (TryCommitText(value)) { _textValue = value; OnPropertyChanged(); }
                else { _textValue = FormatText(_meta.GetValue(_component)); OnPropertyChanged(); }
            }
        }

        public bool BoolValue
        {
            get => _boolValue;
            set { if (_boolValue == value) return; _boolValue = value; CommitBool(value); OnPropertyChanged(); }
        }

        public string OptionValue
        {
            get => _optionValue;
            set
            {
                if (_optionValue == value) return;
                if (value != null)
                {
                    _meta.SetValue(_component, ConvertToType(value));
                    _optionValue = _meta.GetValue(_component)?.ToString();
                }
                OnPropertyChanged();
            }
        }

        private bool TryCommitText(string raw)
        {
            try
            {
                switch (Kind)
                {
                    case EditorKind.Number:
                    {
                        if (string.IsNullOrWhiteSpace(raw)) return false;
                        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return false;
                        if (!double.IsNaN(Min) && d < Min) d = Min;
                        if (!double.IsNaN(Max) && d > Max) d = Max;
                        _meta.SetValue(_component, ConvertToType(d));
                        return Math.Abs((double)ConvertToType(d) - (double)_meta.GetValue(_component)) < 1e-9;
                    }
                    default:
                        _meta.SetValue(_component, ConvertToType(raw));
                        return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void CommitBool(bool value)
        {
            try { _meta.SetValue(_component, value); }
            catch { /* ignore */ }
        }

        private object ConvertToType(object value)
        {
            var t = _meta.PropertyType;
            if (t == typeof(string)) return value?.ToString();
            if (t == typeof(double)) return value is double d ? d : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (t == typeof(int)) return value is int i ? i : Convert.ToInt32(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            if (t == typeof(bool)) return value is bool b ? b : Convert.ToBoolean(value);
            return value;
        }

        private void BrowseFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择文件",
                Filter = "所有文件 (*.*)|*.*|图片 (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp",
            };
            if (dlg.ShowDialog() == true)
                TextValue = dlg.FileName;
        }
    }

    /// <summary>属性面板按分类分组后的组</summary>
    public class PropertyGroupViewModel
    {
        public string Category { get; set; }
        public List<PropertyRowViewModel> Rows { get; set; } = new();
    }
}
