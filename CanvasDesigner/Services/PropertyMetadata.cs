using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CanvasDesigner.Services
{
    /// <summary>一个可编辑/可序列化属性的元数据</summary>
    public class PropertyMeta
    {
        public string Name { get; set; }
        public Type PropertyType { get; set; }
        public PropertyInfo Info { get; set; }
        public Models.EditorPropAttribute Attr { get; set; }
        public bool CanWrite => Info != null && Info.CanWrite && Info.SetMethod != null && !Info.SetMethod.IsStatic;

        public object GetValue(object target) => Info.GetValue(target);
        public void SetValue(object target, object value) => Info.SetValue(target, value);
    }

    /// <summary>
    /// 基于 [EditorProp] 属性 + 反射的组件属性元数据缓存：
    /// 属性面板要展示什么、JSON 要保存什么，都由同一份元数据驱动。
    /// </summary>
    public static class PropertyMetadata
    {
        private static readonly Dictionary<Type, List<PropertyMeta>> _cache = new();

        public static List<PropertyMeta> For(Models.ComponentBase comp) => ForType(comp.GetType());

        public static List<PropertyMeta> ForType(Type type)
        {
            if (_cache.TryGetValue(type, out var cached)) return cached;

            var list = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => Attribute.IsDefined(p, typeof(Models.EditorPropAttribute)))
                .Select(p => new PropertyMeta
                {
                    Name = p.Name,
                    PropertyType = p.PropertyType,
                    Info = p,
                    Attr = (Models.EditorPropAttribute)Attribute.GetCustomAttribute(p, typeof(Models.EditorPropAttribute)),
                })
                .OrderBy(m => m.Attr.Category).ThenBy(m => m.Attr.Order)
                .ToList();

            _cache[type] = list;
            return list;
        }
    }
}
