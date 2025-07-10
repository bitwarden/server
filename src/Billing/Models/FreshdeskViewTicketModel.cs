// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Billing.Models;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class FreshdeskViewTicketModel
{
    [JsonPropertyName("spam")]
    public bool? Spam { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("source")]
    public int? Source { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    [JsonPropertyName("support_email")]
    public string SupportEmail { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("description_text")]
    public string DescriptionText { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }
}
