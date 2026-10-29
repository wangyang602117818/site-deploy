using System.Text.Json;

namespace SiteDeploy.Document.Models
{
    public static class JsonSerializerHelper // 建议将类声明为 static
    {
        private static readonly JsonSerializerOptions _defaultOptions = new()
        {
            PropertyNameCaseInsensitive = true, // 忽略大小写
            WriteIndented = false
        };

        /// <summary>
        /// 序列化对象
        /// </summary>
        public static string Serialize(this object? obj, JsonSerializerOptions? options = null)
        {
            if (obj == null)
                return string.Empty;

            try
            {
                return JsonSerializer.Serialize(obj, options ?? _defaultOptions);
            }
            catch (Exception)
            {
                // 可在这里记录日志
                return string.Empty;
            }
        }

        /// <summary>
        /// 反序列化对象（支持引用类型和值类型）
        /// </summary>
        public static T? ToObj<T>(this string? str, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(str))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(str, options ?? _defaultOptions);
            }
            catch (JsonException)
            {
                // 可以在这里加日志 Log.Error($"JSON反序列化失败：{str}");
                return default;
            }
        }
    }
}
