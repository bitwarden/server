// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class InvoiceUpcomingViewModel : BaseMailModel
{
    public decimal AmountDue { get; set; }
    public DateTime DueDate { get; set; }
    public List<string> Items { get; set; }
    public bool MentionInvoices { get; set; }
    public string UpdateBillingInfoUrl { get; set; } = "https://bitwarden.com/help/update-billing-info/";
}
