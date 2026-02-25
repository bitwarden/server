using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Provider.ProviderInvoiceUpcoming;

#nullable enable

/// <summary>
/// Email sent to providers when their upcoming invoice is approaching.
/// </summary>
public class ProviderInvoiceUpcomingMail : BaseMail<ProviderInvoiceUpcomingMailView>
{
    public override string Subject { get; set; } = "Your upcoming Bitwarden invoice";
}

/// <summary>
/// View model for Provider Invoice Upcoming email template.
/// </summary>
public class ProviderInvoiceUpcomingMailView : BaseMailView
{
    public required decimal AmountDue { get; init; }
    public required DateTime DueDate { get; init; }
    public required List<string> Items { get; init; }
    public string? CollectionMethod { get; init; }
    public bool HasPaymentMethod { get; init; }
    public string? PaymentMethodDescription { get; init; }
    public string UpdateBillingInfoUrl { get; init; } = "https://bitwarden.com/help/update-billing-info/";
    public string ContactUrl { get; init; } = "https://bitwarden.com/contact/";
}
