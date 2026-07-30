namespace Bit.Core.Billing.Organizations.Models;

public record OrganizationMetadata(
    bool IsOnSecretsManagerStandalone,
    int OrganizationOccupiedSeats,
    bool HasPaymentMethod)
{
    // HasPaymentMethod defaults to true so that self-hosted instances (where Stripe billing does
    // not apply) are never blocked by a client-side "add a payment method" check.
    public static OrganizationMetadata Default => new OrganizationMetadata(
        false,
        0,
        true);
}
