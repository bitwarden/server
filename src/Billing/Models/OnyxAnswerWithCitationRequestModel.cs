using System.Text.Json.Serialization;
using static Bit.Billing.BillingSettings;

namespace Bit.Billing.Models;

public class OnyxRequestModel
{
    [JsonPropertyName("persona_id")]
    public int PersonaId { get; set; } = 1;

    [JsonPropertyName("retrieval_options")]
    public RetrievalOptions RetrievalOptions { get; set; } = new RetrievalOptions();

    public OnyxRequestModel(OnyxSettings onyxSettings)
    {
        PersonaId = onyxSettings.PersonaId;
        RetrievalOptions.RunSearch = onyxSettings.SearchSettings.RunSearch;
        RetrievalOptions.RealTime = onyxSettings.SearchSettings.RealTime;
    }
}

/// <summary>
/// This is used with the onyx endpoint /query/answer-with-citation
/// which has been deprecated. This can be removed once later
/// </summary>
public class OnyxAnswerWithCitationRequestModel : OnyxRequestModel
{
    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = new List<Message>();

    public OnyxAnswerWithCitationRequestModel(string message, OnyxSettings onyxSettings) : base(onyxSettings)
    {
        message = message.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
        Messages = new List<Message>() { new Message() { MessageText = message } };
    }
}

/// <summary>
/// This is used with the onyx endpoint /chat/send-message-simple-api
/// </summary>
public class OnyxSendMessageSimpleApiRequestModel : OnyxRequestModel
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    public OnyxSendMessageSimpleApiRequestModel(string message, OnyxSettings onyxSettings) : base(onyxSettings)
    {
        Message = message.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
    }
}

public class Message
{
    [JsonPropertyName("message")]
    public string MessageText { get; set; } = string.Empty;

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = "user";
}

public class RetrievalOptions
{
    [JsonPropertyName("run_search")]
    public string RunSearch { get; set; } = RetrievalOptionsRunSearch.Auto;

    [JsonPropertyName("real_time")]
    public bool RealTime { get; set; } = true;
}

public class RetrievalOptionsRunSearch
{
    public const string Always = "always";
    public const string Never = "never";
    public const string Auto = "auto";
}
