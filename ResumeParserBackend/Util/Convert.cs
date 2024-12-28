using System.Xml;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization.NamingConventions;

namespace ResumeParserBackend.Util;

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

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

    public static string JsonToYaml(string jsonString)
    {
        // 校验输入是否为空
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            throw new ArgumentException("JSON 字符串不能为空或仅包含空白字符。");
        }

        try
        {
            // 将 JSON 反序列化为动态对象
            var jsonObject = JsonConvert.DeserializeObject<object>(jsonString);

            if (jsonObject == null)
            {
                throw new InvalidOperationException("JSON 数据无效或格式不正确，无法解析。");
            }

            // 使用 YamlDotNet 序列化器将对象转换为 YAML
            var serializer = new SerializerBuilder().Build();
            return serializer.Serialize(jsonObject);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("JSON 格式无效，无法转换为 YAML。", ex);
        }
        catch (Exception ex)
        {
            throw new Exception("转换过程中发生未知错误。", ex);
        }
    }
}