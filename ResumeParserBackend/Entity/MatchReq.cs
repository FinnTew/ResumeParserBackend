namespace ResumeParserBackend.Entity;

public class MatchReq
{
    public List<string> Must { get; set; }
    
    public List<string> Should { get; set; }
}