using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ResumeParserBackend.Collection;

public class JobMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    public string JobId { get; set; }
    
    public BsonDocument Metadata { get; set; }
}