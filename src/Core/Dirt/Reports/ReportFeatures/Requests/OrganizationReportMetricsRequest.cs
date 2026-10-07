using System.Text.Json.Serialization;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class OrganizationReportMetricsRequest
{
    [JsonPropertyName("totalApplicationCount")]
    public int? ApplicationCount { get; set; } = null;
    [JsonPropertyName("totalAtRiskApplicationCount")]
    public int? ApplicationAtRiskCount { get; set; } = null;
    [JsonPropertyName("totalCriticalApplicationCount")]
    public int? CriticalApplicationCount { get; set; } = null;
    [JsonPropertyName("totalCriticalAtRiskApplicationCount")]
    public int? CriticalApplicationAtRiskCount { get; set; } = null;
    [JsonPropertyName("totalMemberCount")]
    public int? MemberCount { get; set; } = null;
    [JsonPropertyName("totalAtRiskMemberCount")]
    public int? MemberAtRiskCount { get; set; } = null;
    [JsonPropertyName("totalCriticalMemberCount")]
    public int? CriticalMemberCount { get; set; } = null;
    [JsonPropertyName("totalCriticalAtRiskMemberCount")]
    public int? CriticalMemberAtRiskCount { get; set; } = null;
    [JsonPropertyName("totalPasswordCount")]
    public int? PasswordCount { get; set; } = null;
    [JsonPropertyName("totalAtRiskPasswordCount")]
    public int? PasswordAtRiskCount { get; set; } = null;
    [JsonPropertyName("totalCriticalPasswordCount")]
    public int? CriticalPasswordCount { get; set; } = null;
    [JsonPropertyName("totalCriticalAtRiskPasswordCount")]
    public int? CriticalPasswordAtRiskCount { get; set; } = null;
}
