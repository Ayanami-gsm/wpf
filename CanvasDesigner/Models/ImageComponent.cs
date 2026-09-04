namespace CanvasDesigner.Models
{
    /// <summary>图片（引用本地文件，运行/保存时以相对引用存储，读取时按原样）</summary>
    public class ImageComponent : ComponentBase
    {
        private string _sourcePath = "";
        private string _stretch = "Uniform";   // None / Fill / Uniform / UniformToFill

        public ImageComponent() { Width = 160; Height = 120; }

        [EditorProp("图片路径", EditorKind.File, "内容", 0)]
        public string SourcePath { get => _sourcePath; set => Set(ref _sourcePath, value); }

        [EditorProp("拉伸模式", EditorKind.Options, "外观", 1,
            Options = new[] { "None", "Fill", "Uniform", "UniformToFill" })]
        public string Stretch { get => _stretch; set => Set(ref _stretch, value); }
    }
}
