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
    public string CollectionMethod { get; set; }
    public bool HasPaymentMethod { get; set; }
    public string PaymentMethodDescription { get; set; }
    public string HelpUrl { get; set; } = "https://bitwarden.com/help/";
    public string ContactUrl { get; set; } = "https://bitwarden.com/contact/";
}
