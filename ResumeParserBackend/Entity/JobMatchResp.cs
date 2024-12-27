namespace ResumeParserBackend.Entity;

public class JobMatchResp
{
    public string Id { get; set; }
    
    public double TextSimilarity { get; set; }
    
    public double StructureScore { get; set; }
    
    public double TotalScore { get; set; }
}