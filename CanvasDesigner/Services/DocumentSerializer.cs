using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanvasDesigner.Models;

namespace CanvasDesigner.Services
{
    /// <summary>
    /// 画布文档 JSON 存取。
    /// 结构：
    /// {
    ///   "version": 1,
    ///   "canvasWidth": 1280, "canvasHeight": 720,
    ///   "backgroundColor": "#FF263238", "showGrid": true, "gridSize": 20,
    ///   "items": [ { "type": "RectangleComponent", "Name": "...", "X":0, "Y":0, ..., "Fill":"#..." }, ... ]
    /// }
    /// type 字段由组件目录注册表解析，属性按 [EditorProp] 标注的公开属性落盘。
    /// </summary>
    public static class DocumentSerializer
    {
        // ==================== 画布文档 ====================

        public static string SerializeDocument(CanvasDocument doc)
        {
            var root = new JsonObject
            {
                ["version"] = doc.Version,
                ["canvasWidth"] = doc.CanvasWidth,
                ["canvasHeight"] = doc.CanvasHeight,
                ["backgroundColor"] = doc.BackgroundColor,
                ["showGrid"] = doc.ShowGrid,
                ["gridSize"] = doc.GridSize,
            };

            var items = new JsonArray();
            foreach (var item in doc.Items)
                items.Add(BuildComponentObject(item));
            root["items"] = items;

            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        public static CanvasDocument DeserializeDocument(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new CanvasDocument();

            result.Version = GetInt(root, "version", 1);
            result.CanvasWidth = GetDouble(root, "canvasWidth", 1280);
            result.CanvasHeight = GetDouble(root, "canvasHeight", 720);
            result.BackgroundColor = GetString(root, "backgroundColor", "#FF263238");
            result.ShowGrid = GetBool(root, "showGrid", true);
            result.GridSize = GetInt(root, "gridSize", 20);

            if (root.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var itemEl in itemsEl.EnumerateArray())
                {
                    try
                    {
                        var comp = DeserializeComponentFromElement(itemEl);
                        if (comp != null) result.Items.Add(comp);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("跳过无法识别的部件: " + ex.Message);
                    }
                }
            }
            return result;
        }

        public static void Save(CanvasDocument doc, string filePath)
            => File.WriteAllText(filePath, SerializeDocument(doc), new UTF8Encoding(false));

        public static CanvasDocument Load(string filePath)
            => DeserializeDocument(File.ReadAllText(filePath, new UTF8Encoding(false)));

        // ==================== 单个组件（复制粘贴 / 克隆用） ====================

        public static string SerializeComponent(ComponentBase comp)
            => BuildComponentObject(comp).ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        public static ComponentBase DeserializeComponent(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return DeserializeComponentFromElement(doc.RootElement);
        }

        // ==================== 内部实现 ====================

        private static JsonObject BuildComponentObject(ComponentBase comp)
        {
            var obj = new JsonObject { ["type"] = comp.GetType().Name };
            foreach (var prop in PropertyMetadata.For(comp))
            {
                if (!prop.CanWrite) continue;
                object value = prop.GetValue(comp);
                if (value == null) continue;
                switch (value)
                {
                    case double d: obj[prop.Name] = d; break;
                    case int i:    obj[prop.Name] = i; break;
                    case bool b:   obj[prop.Name] = b; break;
                    case string s: obj[prop.Name] = s; break;
                    default:       obj[prop.Name] = JsonSerializer.SerializeToNode(value)?.DeepClone(); break;
                }
            }
            return obj;
        }

        private static ComponentBase DeserializeComponentFromElement(JsonElement el)
        {
            if (!el.TryGetProperty("type", out var typeEl))
                return null;

            var entry = ComponentCatalog.GetByKey(typeEl.GetString());
            if (entry == null)
                return null;

            var comp = entry.CreateInstance();
            foreach (var prop in PropertyMetadata.For(comp))
            {
                if (!prop.CanWrite) continue;
                if (!el.TryGetProperty(prop.Name, out var valEl)) continue;

                try
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    object converted;
                    if (targetType == typeof(double))
                        converted = valEl.ValueKind == JsonValueKind.String
                            ? double.Parse(valEl.GetString(), CultureInfo.InvariantCulture)
                            : valEl.GetDouble();
                    else if (targetType == typeof(int))
                        converted = valEl.ValueKind == JsonValueKind.String
                            ? int.Parse(valEl.GetString(), CultureInfo.InvariantCulture)
                            : valEl.GetInt32();
                    else if (targetType == typeof(bool))
                        converted = valEl.GetBoolean();
                    else if (targetType == typeof(string))
                        converted = valEl.GetString();
                    else
                        converted = valEl.Deserialize(targetType);
                    prop.SetValue(comp, converted);
                }
                catch (Exception)
                {
                    // 单个属性解析失败不中断整体
                }
            }
            return comp;
        }

        // ---- JsonElement 读取助手 ----
        private static string GetString(JsonElement root, string name, string def)
            => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : def;

        private static double GetDouble(JsonElement root, string name, double def)
        {
            if (!root.TryGetProperty(name, out var v)) return def;
            return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
        }

        private static int GetInt(JsonElement root, string name, int def)
        {
            if (!root.TryGetProperty(name, out var v)) return def;
            return v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
        }

        private static bool GetBool(JsonElement root, string name, bool def)
        {
            if (!root.TryGetProperty(name, out var v)) return def;
            return v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.False && v.GetBoolean());
        }
    }
}
