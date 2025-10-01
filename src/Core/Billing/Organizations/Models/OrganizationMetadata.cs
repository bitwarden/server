namespace Bit.Core.Billing.Organizations.Models;

public record OrganizationMetadata(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone,
    int OrganizationOccupiedSeats)
{
    public static OrganizationMetadata Default => new OrganizationMetadata(
        false,
        false,
        false,
        0);
}
