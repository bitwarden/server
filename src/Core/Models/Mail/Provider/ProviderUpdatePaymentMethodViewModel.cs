﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail.Provider;

public class ProviderUpdatePaymentMethodViewModel : BaseMailModel
{
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string ProviderName { get; set; }

    public string PaymentMethodUrl =>
        $"{WebVaultUrl}/organizations/{OrganizationId}/billing/payment-method";
}
