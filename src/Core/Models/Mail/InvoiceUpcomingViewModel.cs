namespace Bit.Core.Models.Mail;

public class InvoiceUpcomingViewModel : BaseMailModel
{
    public decimal AmountDue { get; set; }
    public DateTime DueDate { get; set; }
    public List<string> Items { get; set; }
    public bool MentionInvoices { get; set; }
}
