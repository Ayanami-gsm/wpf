using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CanvasDesigner.Models;

namespace CanvasDesigner.Services
{
    /// <summary>
    /// 无界面自测：验证“组件目录齐全、属性面板元数据可用、JSON 可往返”。
    /// 调用：dotnet run -- --selftest
    /// 结果写入 selftest.txt
    /// </summary>
    public static class SelfTest
    {
        public static int Run()
        {
            var sb = new StringBuilder();
            int failures = 0;

            void Check(bool ok, string message)
            {
                sb.AppendLine((ok ? "PASS  " : "FAIL  ") + message);
                if (!ok) failures++;
            }

            sb.AppendLine("==== CanvasDesigner SelfTest ====");

            // 1. 目录齐全
            var entries = ComponentCatalog.All.ToList();
            Check(entries.Count == 7, $"目录含 7 种图元（实际 {entries.Count}）");
            foreach (var e in entries)
                Check(ComponentCatalog.GetByKey(e.ComponentType.Name) != null, $"注册可解析: {e.DisplayName} ({e.ComponentType.Name})");

            // 2. 每种图元：无 UI 实例化、几何/外观属性元数据可用
            var doc = new CanvasDocument();
            var created = new List<ComponentBase>();
            int seq = 1;
            foreach (var e in entries)
            {
                var c = e.CreateInstance();
                c.Name = e.DisplayName + (seq++);
                c.X = seq * 20; c.Y = seq * 10;
                created.Add(c);
                doc.Items.Add(c);

                var metas = PropertyMetadata.For(c);
                Check(metas.Count > 0, $"{e.DisplayName} 属性元数据 {metas.Count} 条");
                foreach (var m in metas)
                    Check(m.CanWrite, $"{e.DisplayName}.{m.Name} 可写");
            }

            // 3. 修改若干属性后序列化
            var rect = (RectangleComponent)created[0];
            rect.Fill = "#FF112233"; rect.CornerRadius = 12;
            var lamp = (LampComponent)created.First(x => x is LampComponent);
            lamp.IsOn = true; lamp.Label = "测试灯";

            string json = DocumentSerializer.SerializeDocument(doc);
            Check(!string.IsNullOrWhiteSpace(json), "序列化非空");

            // 4. 反序列化往返
            var doc2 = DocumentSerializer.DeserializeDocument(json);
            Check(doc2.Items.Count == created.Count, $"往返组件数一致（{doc2.Items.Count}）");
            var rect2 = (RectangleComponent)doc2.Items[0];
            Check(rect2.Fill == "#FF112233", $"Fill 往返一致（{rect2.Fill}）");
            Check(Math.Abs(rect2.CornerRadius - 12) < 0.001, $"CornerRadius 往返一致（{rect2.CornerRadius}）");
            var lamp2 = (LampComponent)doc2.Items.First(x => x is LampComponent);
            Check(lamp2.IsOn == true && lamp2.Label == "测试灯", "Lamp 状态往返一致");

            // 5. 单组件克隆（复制命令依赖）
            var cloneJson = DocumentSerializer.SerializeComponent(lamp);
            var lampClone = DocumentSerializer.DeserializeComponent(cloneJson) as LampComponent;
            Check(lampClone != null && lampClone.Label == "测试灯", "单组件克隆往返一致");

            // 6. 未知 type 容错
            var badJson = json.Replace("\"type\": \"LampComponent\"", "\"type\": \"NotFoundXXX\"");
            var doc3 = DocumentSerializer.DeserializeDocument(badJson);
            Check(doc3.Items.Count == created.Count - 1, $"未知类型部件被跳过（{doc3.Items.Count}）");

            // 写出样例 json 供人工检查
            string sample = Path.Combine(AppContext.BaseDirectory, "sample.json");
            File.WriteAllText(sample, json, new UTF8Encoding(false));

            sb.AppendLine($"结果: {(failures == 0 ? "全部通过" : failures + " 项失败")}");
            sb.AppendLine($"样例文件: {sample}");

            string report = Path.Combine(AppContext.BaseDirectory, "selftest.txt");
            File.WriteAllText(report, sb.ToString(), new UTF8Encoding(false));
            return failures == 0 ? 0 : 1;
        }
    }
}
