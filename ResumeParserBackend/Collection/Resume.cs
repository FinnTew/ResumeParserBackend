namespace ResumeParserBackend.Collection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

public class Resume
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    public string ResumeId { get; set; }
    
    public string FilePath { get; set; }
    
    public string OriginalFileName { get; set; }
    
    public string UploadedFileName { get; set; }
    
    public string FileFormat { get; set; }
    
    public DateTime UploadTime { get; set; }
    
    public string ParseStatus { get; set; }
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string ResumeContentCollection { get; set; }
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string ResumeMetadataCollection { get; set; }
}