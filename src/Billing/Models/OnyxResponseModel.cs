using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class OnyxResponseModel
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("answer_citationless")]
    public string AnswerCitationless { get; set; } = string.Empty;

    [JsonPropertyName("error_msg")]
    public string ErrorMsg { get; set; } = string.Empty;
}
