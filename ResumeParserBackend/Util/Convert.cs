namespace ResumeParserBackend.Util;

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

public class Convert
{
    public static string JsonToXml(string jsonString)
    {
        var xmlDocument = JsonConvert.DeserializeXmlNode(jsonString, "root");
        return xmlDocument.OuterXml;
    }

    public static string JsonToCsv(string jsonString)
    {
        var jsonArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonString);
        var dataTable = new DataTable();
        
        jsonArray.ForEach(dict =>
        {
            for (var e = dict.Keys.GetEnumerator(); e.MoveNext(); e.MoveNext())
            {
                if (!dataTable.Columns.Contains(e.Current))
                {
                    dataTable.Columns.Add(e.Current);
                }
            }
        });

        foreach (var dict in jsonArray)
        {
            var row = dataTable.NewRow();
            foreach (var key in dict.Keys)
            {
                row[key] = dict[key];
            }
            dataTable.Rows.Add(row);
        }

        var csvBuilder = new StringBuilder();
        foreach (DataColumn column in dataTable.Columns)
        {
            csvBuilder.Append(column.ColumnName + ",");
        }
        csvBuilder.Length--; // Remove last comma
        csvBuilder.AppendLine();

        foreach (DataRow row in dataTable.Rows)
        {
            foreach (var item in row.ItemArray)
            {
                csvBuilder.Append(item + ",");
            }
            csvBuilder.Length--; // Remove last comma
            csvBuilder.AppendLine();
        }

        return csvBuilder.ToString();
    }

    public static string JsonToYaml(string jsonString)
    {
        var deserializer = new Deserializer();
        var yamlObject = deserializer.Deserialize(new StringReader(jsonString));
        var serializer = new Serializer();
        return serializer.Serialize(yamlObject);
    }
}