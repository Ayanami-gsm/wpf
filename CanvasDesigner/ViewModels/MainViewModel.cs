using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CanvasDesigner.Models;
using CanvasDesigner.Services;

namespace CanvasDesigner.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly CanvasDocument _document = new();
        private ComponentBase _selected;
        private ComponentCatalogEntry _pendingTool;
        private string _filePath;
        private bool _snapEnabled = true;

        public CanvasDocument Document => _document;

        public ObservableCollection<ComponentBase> Items => _document.Items;

        public IReadOnlyList<ComponentCatalogEntry> Toolbox => ComponentCatalog.All;

        public string[] FontList { get; } = FontsProvider.List;

        public bool SnapEnabled
        {
            get => _snapEnabled;
            set { if (Set(ref _snapEnabled, value)) OnPropertyChanged(nameof(SnapLabel)); }
        }

        public string SnapLabel => SnapEnabled ? "吸附:开" : "吸附:关";

        /// <summary>工具栏当前选中待放置的类型</summary>
        public ComponentCatalogEntry PendingTool
        {
            get => _pendingTool;
            set
            {
                if (Set(ref _pendingTool, value))
                    OnPropertyChanged(nameof(Status));
            }
        }

        public ComponentBase Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                if (_selected != null) _selected.IsSelected = false;
                _selected = value;
                if (_selected != null) _selected.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(Status));
                RefreshPropertyGroups();
            }
        }

        public bool HasSelection => Selected != null;

        public string Status
        {
            get
            {
                if (PendingTool != null) return $"待放置: {PendingTool.DisplayName}（在画布空白处单击放置）";
                if (Selected != null) return $"选中: {Selected.Name} ({Selected.TypeLabel})  X={Selected.X:0} Y={Selected.Y:0}";
                return $"组件数: {Items.Count}";
            }
        }

        public ObservableCollection<PropertyGroupViewModel> PropertyGroups { get; } = new();

        // ---------------- 命令 ----------------
        public ICommand NewCommand => new RelayCommand(_ => NewDocument());
        public ICommand OpenCommand => new RelayCommand(_ => OpenDocument());
        public ICommand SaveCommand => new RelayCommand(_ => SaveDocument());
        public ICommand SaveAsCommand => new RelayCommand(_ => SaveAsDocument());
        public ICommand DeleteCommand => new RelayCommand(_ => DeleteSelected(), _ => Selected != null);
        public ICommand CopyCommand => new RelayCommand(_ => CopySelected(), _ => Selected != null);
        public ICommand LayerUpCommand => new RelayCommand(_ => MoveLayer(-1), _ => Selected != null);
        public ICommand LayerDownCommand => new RelayCommand(_ => MoveLayer(+1), _ => Selected != null);
        public ICommand TopCommand => new RelayCommand(_ => MoveToTop(), _ => Selected != null);
        public ICommand BottomCommand => new RelayCommand(_ => MoveToBottom(), _ => Selected != null);
        public ICommand EscapeCommand => new RelayCommand(_ => PendingTool = null);

        public MainViewModel()
        {
            // 演示画面：让首次打开不空白
            CreateDemo();
            AttachItemsChange();
        }

        private void AttachItemsChange()
        {
            Items.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(Items));
            };
        }

        // ---------------- 增删与图层 ----------------
        public ComponentBase AddComponent(ComponentCatalogEntry entry, double x, double y)
        {
            var comp = entry.CreateInstance();
            comp.Name = NextName(entry);
            comp.X = Math.Max(0, x);
            comp.Y = Math.Max(0, y);
            comp.Width = entry.DefaultWidth;
            comp.Height = entry.DefaultHeight;
            comp.ZIndex = Items.Count;
            Items.Add(comp);
            Selected = comp;
            return comp;
        }

        private string NextName(ComponentCatalogEntry entry)
        {
            int n = Items.Count(c => c.TypeLabel == entry.DisplayName) + 1;
            // 兜底：带全局序号保证唯一
            while (Items.Any(c => c.Name == entry.DisplayName + n)) n++;
            return entry.DisplayName + n;
        }

        public void DeleteSelected()
        {
            if (Selected == null) return;
            var s = Selected;
            Selected = null;
            Items.Remove(s);
        }

        public void CopySelected()
        {
            if (Selected == null) return;
            var clone = Selected.Clone();
            clone.Name = Selected.Name + "_副本";
            clone.X += 20;
            clone.Y += 20;
            clone.ZIndex = Items.Count;
            Items.Add(clone);
            Selected = clone;
        }

        public void MoveLayer(int direction)
        {
            if (Selected == null) return;
            int idx = Items.IndexOf(Selected);
            int target = idx + direction;
            if (target < 0 || target >= Items.Count) return;
            Items.Move(idx, target);
            RefreshZ();
        }

        public void MoveToTop()
        {
            if (Selected == null) return;
            Items.Move(Items.IndexOf(Selected), Items.Count - 1);
            RefreshZ();
        }

        public void MoveToBottom()
        {
            if (Selected == null) return;
            Items.Move(Items.IndexOf(Selected), 0);
            RefreshZ();
        }

        private void RefreshZ()
        {
            for (int i = 0; i < Items.Count; i++) Items[i].ZIndex = i;
        }

        public double Snap(double v) => Math.Round(v / _document.GridSize) * _document.GridSize;

        // ---------------- 文档 ----------------
        public string FilePath => _filePath;
        public string Title => _filePath == null ? "CanvasDesigner - 通用画面编辑器" : $"CanvasDesigner - {Path.GetFileName(_filePath)}";

        public void NewDocument()
        {
            _filePath = null;
            _document.Items.Clear();
            OnDocumentChanged();
            Selected = null;
        }

        public void OpenDocument()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "画面 JSON (*.json)|*.json|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var doc = DocumentSerializer.Load(dlg.FileName);
                ApplyDocument(doc);
                _filePath = dlg.FileName;
                OnDocumentChanged();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("打开失败：" + ex.Message, "CanvasDesigner");
            }
        }

        public void SaveDocument()
        {
            if (_filePath == null) { SaveAsDocument(); return; }
            try { DocumentSerializer.Save(_document, _filePath); }
            catch (Exception ex) { System.Windows.MessageBox.Show("保存失败：" + ex.Message, "CanvasDesigner"); }
        }

        public void SaveAsDocument()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "画面 JSON (*.json)|*.json", FileName = "画面.json" };
            if (dlg.ShowDialog() != true) return;
            _filePath = dlg.FileName;
            try { DocumentSerializer.Save(_document, _filePath); }
            catch (Exception ex) { System.Windows.MessageBox.Show("保存失败：" + ex.Message, "CanvasDesigner"); }
        }

        private void ApplyDocument(CanvasDocument doc)
        {
            _document.CanvasWidth = doc.CanvasWidth;
            _document.CanvasHeight = doc.CanvasHeight;
            _document.BackgroundColor = doc.BackgroundColor;
            _document.ShowGrid = doc.ShowGrid;
            _document.GridSize = doc.GridSize;
            _document.Items.Clear();
            foreach (var c in doc.Items) _document.Items.Add(c);
            RefreshZ();
        }

        private void OnDocumentChanged()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(Status));
            RefreshPropertyGroups();
        }

        // ---------------- 属性面板 ----------------
        private void RefreshPropertyGroups()
        {
            PropertyGroups.Clear();
            if (Selected == null) return;

            var metas = PropertyMetadata.For(Selected);
            var groups = metas
                .GroupBy(m => m.Attr.Category)
                .Select(g => new PropertyGroupViewModel
                {
                    Category = g.Key,
                    Rows = g.Select(m => new PropertyRowViewModel(Selected, m)).ToList(),
                })
                .ToList();

            foreach (var g in groups) PropertyGroups.Add(g);

            Selected.PropertyChanged -= OnSelectedPropertyChanged;
            Selected.PropertyChanged += OnSelectedPropertyChanged;
        }

        private void OnSelectedPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            foreach (var g in PropertyGroups)
                foreach (var row in g.Rows)
                    if (row.PropertyName == e.PropertyName)
                    {
                        row.RefreshFromComponent();
                        OnPropertyChanged(nameof(Status));
                    }
        }

        // ---------------- 演示画面 ----------------
        private void CreateDemo()
        {
            var txt = AddComponent(ComponentCatalog.GetByKey("TextComponent"), 420, 20);
            if (txt is TextComponent t) { t.Text = "通用画面编辑器 - 示例"; t.FontSize = 30; t.HAlign = "Center"; }
            txt.Width = 440; txt.Height = 50; txt.Y = 20;

            var rect = AddComponent(ComponentCatalog.GetByKey("RectangleComponent"), 80, 120);
            if (rect is RectangleComponent rc) { rc.Fill = "#FF37474F"; rc.CornerRadius = 6; }
            rect.Width = 300; rect.Height = 180;

            var ell = AddComponent(ComponentCatalog.GetByKey("EllipseComponent"), 140, 150);
            if (ell is EllipseComponent ec) { ec.Fill = "#FF00838F"; }
            ell.Width = 180; ell.Height = 120;

            var lamp = AddComponent(ComponentCatalog.GetByKey("LampComponent"), 260, 220);
            if (lamp is LampComponent lc) { lc.IsOn = true; lc.Label = "RUN"; }
            lamp.Width = 80; lamp.Height = 96;

            var btn = AddComponent(ComponentCatalog.GetByKey("ButtonComponent"), 80, 340);
            if (btn is ButtonComponent bc) { bc.Text = "启动"; bc.Background = "#FF2E7D32"; }
            btn.Width = 120; btn.Height = 46;

            var btn2 = AddComponent(ComponentCatalog.GetByKey("ButtonComponent"), 220, 340);
            if (btn2 is ButtonComponent bc2) { bc2.Text = "停止"; bc2.Background = "#FFB71C1C"; }
            btn2.Width = 120; btn2.Height = 46;

            var line = AddComponent(ComponentCatalog.GetByKey("LineComponent"), 400, 130);
            line.Width = 200; line.Height = 4;
            if (line is LineComponent lc2) lc2.Stroke = "#FF80CBC4";

            var note = AddComponent(ComponentCatalog.GetByKey("TextComponent"), 600, 110);
            if (note is TextComponent nt)
            {
                nt.Text = "试试：双击左侧图元添加\n拖动图元 / 拖角缩放\n在右侧编辑属性\nCtrl+S 保存为 JSON";
                nt.FontSize = 15; nt.HAlign = "Left"; nt.VAlign = "Top";
            }
            note.Width = 380; note.Height = 120;

            Selected = null;
            RefreshZ();
        }
    }

    /// <summary>字体候选（含常见中英文字体，避免无中文字体）</summary>
    public static class FontsProvider
    {
        public static readonly string[] List =
        {
            "Microsoft YaHei", "Microsoft YaHei UI", "SimSun", "SimHei", "KaiTi", "FangSong",
            "Arial", "Segoe UI", "Tahoma", "Times New Roman", "Consolas", "Courier New",
        };
    }
}
