using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CanvasDesigner.Models
{
    /// <summary>一个可保存/加载的画布文档（类似 Linear 的 *.json 布局文件）</summary>
    public class CanvasDocument
    {
        public const string FileExtension = ".json";

        public int Version { get; set; } = 1;

        /// <summary>画布尺寸</summary>
        public double CanvasWidth { get; set; } = 1280;
        public double CanvasHeight { get; set; } = 720;

        /// <summary>画布背景（#AARRGGBB）</summary>
        public string BackgroundColor { get; set; } = "#FF263238";

        public bool ShowGrid { get; set; } = true;
        public int GridSize { get; set; } = 20;

        /// <summary>组件列表（保持画布上的堆叠顺序）</summary>
        public ObservableCollection<ComponentBase> Items { get; set; } = new();

        /// <summary>深拷贝一份空文档</summary>
        public CanvasDocument CloneSettings()
            => new()
            {
                CanvasWidth = CanvasWidth,
                CanvasHeight = CanvasHeight,
                BackgroundColor = BackgroundColor,
                ShowGrid = ShowGrid,
                GridSize = GridSize,
            };
    }
}
