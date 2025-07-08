// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;

namespace Bit.Billing.Models;

public class FreshdeskWebhookModel
{
    [JsonPropertyName("ticket_id")]
    public string TicketId { get; set; }

    [JsonPropertyName("ticket_contact_email")]
    public string TicketContactEmail { get; set; }

    [JsonPropertyName("ticket_tags")]
    public string TicketTags { get; set; }
}
