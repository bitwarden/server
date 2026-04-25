using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Provider.ProviderUpdatePaymentMethod;

#nullable enable

/// <summary>
/// Email sent to organization owners when their organization is removed from a provider,
/// asking them to update their billing payment method.
/// </summary>
public class ProviderUpdatePaymentMethodMail : BaseMail<ProviderUpdatePaymentMethodMailView>
{
    public override string Subject { get; set; } = "Update your billing information";
}

/// <summary>
/// View model for Provider Update Payment Method email template.
/// </summary>
public class ProviderUpdatePaymentMethodMailView : BaseMailView
{
    public required string OrganizationId { get; init; }
    public required string OrganizationName { get; init; }
    public required string ProviderName { get; init; }
    public required string WebVaultUrl { get; init; }

    public string PaymentMethodUrl =>
        $"{WebVaultUrl}/organizations/{OrganizationId}/billing/payment-method";
}
