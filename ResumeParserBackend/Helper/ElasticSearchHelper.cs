using Nest;

namespace ResumeParserBackend.Helper;

public sealed class ElasticSearchSingleton
{
    private static readonly Lazy<ElasticSearchSingleton> _instance = new(() => new ElasticSearchSingleton());

    private readonly ElasticClient _client;

    private ElasticSearchSingleton()
    {
        var node = new Uri($"http://{ConfigManager.Instance.Get(c => c.Es.Host)}:{ConfigManager.Instance.Get(c => c.Es.Port)}/");
        var settings = new ConnectionSettings(node)
            .DefaultIndex(ConfigManager.Instance.Get(c => c.Es.Index))
            .DisableDirectStreaming();
        
        _client = new ElasticClient(settings);
    }
    
    public static ElasticSearchSingleton Instance => _instance.Value;
    
    public ElasticClient Client => _client;
}

public class ElasticSearchHelper
{
    private readonly ElasticClient _client = ElasticSearchSingleton.Instance.Client;

    public async Task InitializeDataAsync<T>(string indexName, IEnumerable<T> data) where T : class
    {
        // 检查索引是否存在
        var existsResponse = await _client.Indices.ExistsAsync(indexName);
        if (!existsResponse.Exists)
        {
            // 如果索引不存在，创建索引
            var createResponse = await _client.Indices.CreateAsync(indexName, c => c
                    .Map<T>(m => m.AutoMap()) // 自动映射字段
            );

            if (!createResponse.IsValid)
            {
                throw new Exception($"Failed to create index [{indexName}]: {createResponse.DebugInformation}");
            }
        }

        // 批量插入数据
        var bulkResponse = await _client.BulkAsync(b => b
            .Index(indexName)
            .IndexMany(data)
        );

        if (!bulkResponse.IsValid)
        {
            throw new Exception($"Failed to create index [{indexName}]: {bulkResponse.DebugInformation}");
        }
    }

    public async Task InsertDataAsync<T>(string indexName, T data) where T : class
    {
        var existsResponse = await _client.Indices.ExistsAsync(indexName);
        if (!existsResponse.Exists)
        {
            var createResponse = await _client.Indices.CreateAsync(indexName, c => c
                .Map<T>(m => m.AutoMap()));
            if (!createResponse.IsValid)
            {
                throw new Exception($"Failed to create index [{indexName}]: {createResponse.DebugInformation}");
            }
        }

        var indexResponse = await _client.IndexAsync(data, i => i.Index(indexName));
        if (!indexResponse.IsValid)
        {
            throw new Exception($"Failed to index document: {indexResponse.DebugInformation}");
        }
    }
    
    public async Task<bool> ClearAllDataAsync(string indexName)
    {
        var response = await _client.Indices.DeleteAsync(indexName);

        if (!response.IsValid)
        {
            throw new Exception($"Failed to delete index [{indexName}]: {response.DebugInformation}");
        }
        
        return response.IsValid;
    }
    
    public async Task<ISearchResponse<T>> BoolSearchAsync<T>(string indexName, 
        Func<QueryContainerDescriptor<T>, QueryContainer> mustQuery, 
        Func<QueryContainerDescriptor<T>, QueryContainer> shouldQuery) where T : class
    {
        var response = await _client.SearchAsync<T>(s => s
            .Index(indexName)
            .Query(q => q
                .Bool(b => b
                        .Must(mustQuery) // 必须满足的条件
                        .Should(shouldQuery) // 可以满足的条件
                        .MinimumShouldMatch(1) // 至少满足一个 should 条件
                )
            )
        );

        if (!response.IsValid)
        {
            throw new Exception($"Failed to query string [{indexName}]: {response.DebugInformation}");
        }

        return response;
    }
}