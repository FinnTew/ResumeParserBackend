namespace ResumeParserBackend.Entity;

public class ResumeEntity
{
    public string ResumeId { get; set; }
    
    public string OriginFileName { get; set; }
    
    public DateTime UploadTime { get; set; }
    
    public string ParseStatus { get; set; }
}