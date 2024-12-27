using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

namespace ResumeParserBackend.Util;

public class RpcCall
{
    private static readonly string _url = $"{ConfigManager.Instance.Get(c => c.Rpc.Host)}:{ConfigManager.Instance.Get(c => c.Rpc.Port)}";

    public async Task<string> Call(string methodName, params object[] args)
    {
        var request = new 
        {
            jsonrpc = "2.0", 
            method = methodName,
            @params = args,
            id = 1
        };

        var jsonRequest = JsonSerializer.Serialize(request);

        try
        {
            using var client = new HttpClient();
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_url, content);

            var responseJson = await response.Content.ReadAsStringAsync();

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
            return jsonResponse.GetProperty("result").ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message); 
            return "";
        }
    }
}