using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ResumeParserBackend.Collection;

public class Job
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public string JobId { get; set; }
    
    public string JobDescription { get; set; }
    
    public string ParseStatus { get; set; }
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string JobMetadata { get; set; }
}