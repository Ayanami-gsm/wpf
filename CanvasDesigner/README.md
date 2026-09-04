# CanvasDesigner 通用画面编辑器（WPF .NET 9）

借鉴 LinearUI / LinearRT 的“自定义组件”套路（图元 = 可序列化数据类 + 自绘外观 + 属性元数据），
重新实现的独立通用画面编辑器：

- 自由添加组件（单击工具箱条目 → 点画布放置；或双击直接添加到视口中央）
- 画布内拖拽移动，靠近边缘出现箭头光标时拖拽即 8 方向缩放
- 右侧属性面板按分类实时编辑：名称/位置/尺寸/层级、填充色/边框色、文本/字体/字号、
  指示灯开关、图片路径、线段方向等
- 整体保存 / 读取 JSON（文件 → 保存 / 打开），也可另存为
- 深色工业风格界面 + 网格背景 + 可选“吸附”

## 运行

```
dotnet run --project CanvasDesigner.csproj
```

无界面自测（验证 7 种图元 JSON 往返等）：

```
dotnet run --project CanvasDesigner.csproj -- --selftest
# 结果写入 bin\Debug\net9.0-windows\selftest.txt
```

## 项目结构

- `Models/`           图元数据类（ComponentBase + 7 种图元），属性用 [EditorProp] 标注
- `Models/ComponentCatalog.cs`  组件注册表：工具箱与 JSON type 分发共用
- `Services/PropertyMetadata.cs` 基于 [EditorProp] 的属性元数据（驱动属性面板 + 序列化）
- `Services/DocumentSerializer.cs` 画布文档 JSON 存取
- `Controls/DesignerItem.cs`   画布图元宿主：自绘外观 + 点选/拖动/8 向缩放
- `Controls/DesignerSurface.cs` 画布底布（背景 + 网格）
- `Controls/EditorTemplateSelector.cs` 属性编辑器按类型分发
- `MainWindow.xaml`     工具箱 | 画布 | 属性面板

## JSON 文件格式（节选）

```json
{
  "version": 1,
  "canvasWidth": 1280,
  "canvasHeight": 720,
  "backgroundColor": "#FF263238",
  "showGrid": true,
  "gridSize": 20,
  "items": [
    { "type": "RectangleComponent", "Name": "矩形1", "X": 10.0, "Y": 10.0,
      "Width": 160.0, "Height": 100.0, "ZIndex": 0,
      "Fill": "#FF5B9BD5", "Stroke": "#FF2B3A4A", "StrokeThickness": 1.5,
      "CornerRadius": 4.0 }
  ]
}
```

- `type` 字段由 `ComponentCatalog` 解析成对应图元类；
- 未知 type 部件在打开时自动跳过，不影响其它组件；
- 文件可用任意文本编辑器手工修改后再次打开。
