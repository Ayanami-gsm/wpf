using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CanvasDesigner.Controls;
using CanvasDesigner.Models;
using CanvasDesigner.Services;
using CanvasDesigner.ViewModels;

namespace CanvasDesigner
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;
        private readonly Dictionary<ComponentBase, DesignerItem> _itemMap = new();
        private bool _syncing;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            // 工具箱分组
            var toolboxList = new ObservableCollection<ComponentCatalogEntry>(_vm.Toolbox);
            var view = new ListCollectionView(toolboxList)
            {
                GroupDescriptions = { new PropertyGroupDescription(nameof(ComponentCatalogEntry.Group)) }
            };
            ToolboxList.ItemsSource = view;

            _vm.Items.CollectionChanged += Items_CollectionChanged;
            _vm.PropertyChanged += Vm_PropertyChanged;
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SnapEnabled))
                foreach (var it in _itemMap.Values) it.SnapEnabled = _vm.SnapEnabled;
            if (e.PropertyName == nameof(MainViewModel.Status))
                UpdateStatus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SyncCanvas();
            SyncCanvasFrame();
            UpdateStatus();
            FocusManager.SetFocusedElement(this, ToolboxList);
        }

        private void UpdateStatus()
        {
            if (TxtStatus != null)
            {
                TxtStatus.Text = _vm.Status;
                Title = _vm.Title;
            }
            if (TxtSelectedHeader != null)
                TxtSelectedHeader.Text = _vm.Selected == null
                    ? "（无）"
                    : $"{_vm.Selected.TypeLabel} · {_vm.Selected.Name}";
        }

        // ==================== 画布同步 ====================

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncing) return;
            SyncCanvas();
        }

        /// <summary>根据 Items 重建画布上的 DesignerItem</summary>
        private void SyncCanvas()
        {
            _syncing = true;
            try
            {
                foreach (var kv in _itemMap) kv.Key.PropertyChanged -= Item_PropertyChanged;
                _itemMap.Clear();
                Surface.Children.Clear();

                foreach (var comp in _vm.Items)
                {
                    var item = new DesignerItem(comp)
                    {
                        SnapFn = _vm.Snap,
                        SnapEnabled = _vm.SnapEnabled,
                    };
                    item.RequestSelect += (s, _) => { _vm.PendingTool = null; _vm.Selected = comp; };
                    comp.PropertyChanged += Item_PropertyChanged;
                    _itemMap[comp] = item;
                    Surface.Children.Add(item);
                    item.InvalidateMeasure();
                    PositionItem(comp, item);
                }
            }
            finally
            {
                _syncing = false;
            }
            UpdateStatus();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not ComponentBase comp) return;
            if (_itemMap.TryGetValue(comp, out var item) && e.PropertyName == nameof(ComponentBase.ZIndex))
                PositionItem(comp, item);
        }

        private void PositionItem(ComponentBase comp, DesignerItem item)
        {
            Canvas.SetLeft(item, comp.X);
            Canvas.SetTop(item, comp.Y);
            Panel.SetZIndex(item, comp.ZIndex);
        }

        /// <summary>画布尺寸/背景/网格等外部属性的回填</summary>
        private void SyncCanvasFrame()
        {
            Surface.Width = _vm.Document.CanvasWidth;
            Surface.Height = _vm.Document.CanvasHeight;
            Surface.SetLook(_vm.Document.BackgroundColor, _vm.Document.GridSize, _vm.Document.ShowGrid);

            TxtCanvasW.Text = _vm.Document.CanvasWidth.ToString("0.#", CultureInfo.InvariantCulture);
            TxtCanvasH.Text = _vm.Document.CanvasHeight.ToString("0.#", CultureInfo.InvariantCulture);
            TxtGrid.Text = _vm.Document.GridSize.ToString(CultureInfo.InvariantCulture);
            TxtBackground.Text = _vm.Document.BackgroundColor;
            ChkGrid.IsChecked = _vm.Document.ShowGrid;
        }

        // ==================== 画布鼠标：空白处放置/取消选中 ====================

        private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DesignerSurface) return;   // 只有空白画布
            var pos = e.GetPosition(Surface);

            if (_vm.PendingTool != null)
            {
                double x = _vm.SnapEnabled ? _vm.Snap(pos.X) : pos.X;
                double y = _vm.SnapEnabled ? _vm.Snap(pos.Y) : pos.Y;
                var entry = _vm.PendingTool;
                _vm.AddComponent(entry, x, y);   // 保留待放状态以便连续放置
                e.Handled = true;
            }
            else
            {
                _vm.Selected = null;
                e.Handled = true;
            }
        }

        // ==================== 工具箱 ====================

        private void Toolbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.PendingTool = ToolboxList.SelectedItem as ComponentCatalogEntry;
        }

        private void Toolbox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ToolboxList.SelectedItem is not ComponentCatalogEntry entry) return;
            // 双击：直接放在当前可视区中央
            double cx = CanvasScroll.HorizontalOffset + CanvasScroll.ViewportWidth / 2 - entry.DefaultWidth / 2;
            double cy = CanvasScroll.VerticalOffset + CanvasScroll.ViewportHeight / 2 - entry.DefaultHeight / 2;
            _vm.AddComponent(entry, Math.Max(0, cx), Math.Max(0, cy));
            e.Handled = true;
        }

        // ==================== 菜单与按钮 ====================

        private void New_Click(object sender, RoutedEventArgs e)
        {
            _vm.NewDocument();
            SyncCanvasFrame();
            SyncCanvas();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _vm.OpenDocument();
            SyncCanvasFrame();
            SyncCanvas();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _vm.SaveDocument();
            UpdateStatus();
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            _vm.SaveAsDocument();
            UpdateStatus();
        }

        private void Copy_Click(object sender, RoutedEventArgs e) => _vm.CopySelected();
        private void Delete_Click(object sender, RoutedEventArgs e) => _vm.DeleteSelected();
        private void LayerUp_Click(object sender, RoutedEventArgs e) => _vm.MoveLayer(-1);
        private void LayerDown_Click(object sender, RoutedEventArgs e) => _vm.MoveLayer(+1);
        private void Top_Click(object sender, RoutedEventArgs e) => _vm.MoveToTop();
        private void Bottom_Click(object sender, RoutedEventArgs e) => _vm.MoveToBottom();

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "操作说明\n" +
                "· 单击左侧工具箱条目 → 在画布空白处单击放置（Esc 取消待放置）\n" +
                "· 双击工具箱条目 → 添加到当前可视区中央\n" +
                "· 画布内拖拽图元移动；靠近边缘出现箭头时拖拽可缩放（8 方向）\n" +
                "· 右侧属性面板实时编辑名称/位置/颜色/文本等\n" +
                "· Ctrl+S 保存 / Ctrl+O 打开 / Ctrl+N 新建 / Del 删除 / Ctrl+D 复制\n" +
                "· 单击空白处取消选中；勾选“吸附”可对齐网格",
                "CanvasDesigner");
        }

        // ==================== 快捷键 ====================

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (e.Key == Key.Escape) { _vm.PendingTool = null; e.Handled = true; return; }

            if (ctrl && e.Key == Key.N) { New_Click(null, null); e.Handled = true; return; }
            if (ctrl && e.Key == Key.O) { Open_Click(null, null); e.Handled = true; return; }
            if (ctrl && e.Key == Key.S) { Save_Click(null, null); e.Handled = true; return; }
            if (ctrl && e.Key == Key.D) { _vm.CopySelected(); e.Handled = true; return; }
            if (e.Key == Key.Delete)
            {
                // 文本框/组合框内不执行删除图元
                if (Keyboard.FocusedElement is TextBox or ComboBox) return;
                _vm.DeleteSelected();
                e.Handled = true;
                return;
            }
        }

        // ==================== 画布参数控件 ====================

        private void TxtCanvasW_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtCanvasW.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 100)
            {
                _vm.Document.CanvasWidth = v;
                Surface.Width = v;
            }
            SyncCanvasFrame();
        }

        private void TxtCanvasH_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtCanvasH.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 100)
            {
                _vm.Document.CanvasHeight = v;
                Surface.Height = v;
            }
            SyncCanvasFrame();
        }

        private void TxtGrid_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtGrid.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= 4)
                _vm.Document.GridSize = v;
            SyncCanvasFrame();
        }

        private void TxtBackground_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtBackground.Text))
            {
                _vm.Document.BackgroundColor = TxtBackground.Text;
                SyncCanvasFrame();
            }
        }

        private void GridToggled(object sender, RoutedEventArgs e)
        {
            // XAML 初始化阶段 IsChecked 默认触发一次事件，此时 _vm/Surface 尚未就绪
            if (_vm == null || Surface == null) return;
            _vm.Document.ShowGrid = ChkGrid.IsChecked == true;
            if (Surface != null) SyncCanvasFrame();
        }
    }
}
