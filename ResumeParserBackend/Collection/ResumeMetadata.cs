using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ResumeParserBackend.Collection;

public class ResumeMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    public string ResumeId { get; set; }
    
    public BsonDocument Metadata { get; set; }
}