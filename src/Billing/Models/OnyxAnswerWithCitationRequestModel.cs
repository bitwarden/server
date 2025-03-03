﻿
using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class OnyxAnswerWithCitationRequestModel
{
    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; }

    [JsonPropertyName("persona_id")]
    public int PersonaId { get; set; } = 1;

    [JsonPropertyName("prompt_id")]
    public int PromptId { get; set; } = 1;

    [JsonPropertyName("retrieval_options")]
    public RetrievalOptions RetrievalOptions { get; set; }

    public OnyxAnswerWithCitationRequestModel(string message)
    {
        message = message.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
        Messages = new List<Message>() { new Message() { MessageText = message } };
        RetrievalOptions = new RetrievalOptions();
    }
}

public class Message
{
    [JsonPropertyName("message")]
    public string MessageText { get; set; }

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = "user";
}

public class RetrievalOptions
{
    [JsonPropertyName("run_search")]
    public string RunSearch { get; set; } = RetrievalOptionsRunSearch.Auto;

    [JsonPropertyName("real_time")]
    public bool RealTime { get; set; } = true;

    [JsonPropertyName("limit")]
    public int? Limit { get; set; } = 3;
}

public class RetrievalOptionsRunSearch
{
    public const string Always = "always";
    public const string Never = "never";
    public const string Auto = "auto";
}
