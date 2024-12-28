using System.Text.Json.Serialization;

namespace ResumeParserBackend.Entity;

public class FindByDataReq
{
    [JsonPropertyName("start")]
    public long StartTimestamp { get; set; }

    [JsonPropertyName("end")]
    public long EndTimestamp { get; set; }

    [JsonIgnore]
    public DateTime Start => DateTimeOffset.FromUnixTimeSeconds(StartTimestamp).UtcDateTime;

    [JsonIgnore]
    public DateTime End => DateTimeOffset.FromUnixTimeSeconds(EndTimestamp).UtcDateTime;
}