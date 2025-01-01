using System.Text.Json;
using System.Xml;

namespace ResumeParserBackend.Util;

using System.Collections.Generic;
using Newtonsoft.Json;

public class Convert
{
    public static string JsonToXml(string jsonString, string rootName = "Root")
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            throw new ArgumentException("JSON 字符串不能为空或仅包含空白字符。");
        }

        try
        {
            // 使用 Newtonsoft.Json 的 JsonConvert 来解析 JSON 并转换为 XML
            XmlDocument xmlDocument = JsonConvert.DeserializeXmlNode(jsonString, rootName);
            
            // 返回 XML 字符串
            return xmlDocument.OuterXml;
        }
        catch (JsonException jsonEx)
        {
            throw new InvalidOperationException("JSON 格式无效，无法转换为 XML。", jsonEx);
        }
        catch (Exception ex)
        {
            throw new Exception("转换过程中发生未知错误。", ex);
        }
    }

    public static string JsonToToml(string jsonString)
    {
        try
        {
            // 使用 System.Text.Json 解析 JSON 字符串
            var jsonObject = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

            // 手动生成 TOML
            return GenerateToml(jsonObject);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"转换失败: {ex.Message}");
            return null;
        }
    }

    private static string GenerateToml(Dictionary<string, object> dictionary, string prefix = "")
    {
        var toml = new List<string>();

        foreach (var kvp in dictionary)
        {
            string key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    // jsoncase "k" : { "a": ["d", "e"], "b": "c"}
                    case JsonValueKind.Object:
                        var nestedDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                        toml.Add(GenerateToml(nestedDict, key));
                        break;

                    // json  "k" : ["a", "b", ...  ], toml k = [a, b, ...]
                    case JsonValueKind.Array:
                        var arrayItems = System.Text.Json.JsonSerializer.Deserialize<List<object>>(jsonElement.GetRawText());
                        toml.Add($"{key} = [{string.Join(", ", arrayItems)}]");
                        break;

                    case JsonValueKind.String:
                        toml.Add($"{key} = \"{jsonElement.GetString()}\"");
                        break;

                    case JsonValueKind.Number:
                        toml.Add($"{key} = {jsonElement.GetRawText()}");
                        break;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        toml.Add($"{key} = {jsonElement.GetRawText().ToLower()}");
                        break;

                    default:
                        toml.Add($"{key} = null");
                        break;
                }
            }
            else if (kvp.Value is Dictionary<string, object> nested)
            {
                toml.Add(GenerateToml(nested, key));
            }
            else if (kvp.Value is List<object> list)
            {
                toml.Add($"{key} = [{string.Join(", ", list)}]");
            }
            else
            {
                toml.Add($"{key} = {kvp.Value}");
            }
        }

        return string.Join(Environment.NewLine, toml);
    }
}