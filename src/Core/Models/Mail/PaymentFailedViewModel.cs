namespace Bit.Core.Models.Mail;

public class PaymentFailedViewModel : BaseMailModel
{
    public decimal Amount { get; set; }
    public bool MentionInvoices { get; set; }
}
