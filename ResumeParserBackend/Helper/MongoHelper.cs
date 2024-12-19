namespace ResumeParserBackend.Helper;

using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

public sealed class MongoDbSingleton
{
    private static readonly Lazy<MongoDbSingleton> _instance = new(() => new MongoDbSingleton());
    
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    
    private MongoDbSingleton()
    {
        var connString = $"mongodb://{ConfigManager.Instance.Get(c => c.Mongo.Host)}:{ConfigManager.Instance.Get(c => c.Mongo.Port)}";
        _client = new MongoClient(connString);
        _database = _client.GetDatabase(ConfigManager.Instance.Get(c => c.Mongo.Database));
    }
    
    public static MongoDbSingleton Instance => _instance.Value;
    
    public MongoClient Client => _client;
    public IMongoDatabase Database => _database;
}

public class MongoDbHelper<T>(string collectionName)
    where T : class
{
    private readonly IMongoCollection<T> _collection = MongoDbSingleton.Instance.Database.GetCollection<T>(collectionName);

    public async Task InsertOneAsync(T document)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _collection.InsertOneAsync(document);
    }

    public async Task InsertManyAsync(IEnumerable<T> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        await _collection.InsertManyAsync(documents);
    }

    public async Task<T> FindOneAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<T>> FindManyAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<List<T>> FindAllAsync()
    {
        return await _collection.Find(Builders<T>.Filter.Empty).ToListAsync();
    }

    public async Task UpdateOneAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _collection.UpdateOneAsync(filter, update);
    }

    public async Task UpdateManyAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _collection.UpdateManyAsync(filter, update);
    }

    public async Task DeleteOneAsync(Expression<Func<T, bool>> filter)
    {
        await _collection.DeleteOneAsync(filter);
    }

    public async Task DeleteManyAsync(Expression<Func<T, bool>> filter)
    {
        await _collection.DeleteManyAsync(filter);
    }
}