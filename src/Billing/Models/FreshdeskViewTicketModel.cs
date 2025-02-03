namespace Bit.Billing.Models;

public class FreshdeskViewTicketModel
{
    public bool? Spam { get; set; }
    public int? Priority { get; set; }
    public int? Source { get; set; }
    public int? Status { get; set; }
    public string Subject { get; set; }
    public string SupportEmail { get; set; }
    public int Id { get; set; }
    public string Description { get; set; }
    public string DescriptionText { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Tags { get; set; }
}
