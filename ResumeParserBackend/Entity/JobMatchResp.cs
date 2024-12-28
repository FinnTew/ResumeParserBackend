namespace ResumeParserBackend.Entity;

public class JobMatchResp
{
    public string Id { get; set; }
    
    public double TextSimilarity { get; set; }
    
    public double SkillScore { get; set; }
    
    public double TotalScore { get; set; }
}