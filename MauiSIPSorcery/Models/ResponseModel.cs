using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MauiSIPSorcery.Models
{
    public class ResponseModel
    {
        public bool IsSucc { get; set; }
        public string? Msg { get; set; }
        public Dictionary<string, object?>? Data { get; set; }

        public T? GetValue<T>(string key)
        {
            if (Data == null || !Data.TryGetValue(key, out var value) || value == null)
            {
                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            var jsonElement = (JsonElement)value;

            try
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Undefined or JsonValueKind.Null => default,
                    JsonValueKind.True when typeof(T) == typeof(bool) => (T)(object)true,
                    JsonValueKind.False when typeof(T) == typeof(bool) => (T)(object)false,
                    JsonValueKind.Number when typeof(T) == typeof(int) => (T)(object)jsonElement.GetInt32(),
                    JsonValueKind.Number when typeof(T) == typeof(long) => (T)(object)jsonElement.GetInt64(),
                    JsonValueKind.Number when typeof(T) == typeof(double) => (T)(object)jsonElement.GetDouble(),
                    JsonValueKind.Number when typeof(T) == typeof(decimal) => (T)(object)jsonElement.GetDecimal(),
                    JsonValueKind.String when typeof(T) == typeof(string) => (T)(object)jsonElement.GetString()!,
                    JsonValueKind.String when typeof(T) == typeof(Guid) => (T)(object)jsonElement.GetGuid(),
                    JsonValueKind.String when typeof(T) == typeof(DateTime) => (T)(object)jsonElement.GetDateTime(),
                    _ => JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
                };
            }
            catch
            {
                return default;
            }
        }
    }
}
