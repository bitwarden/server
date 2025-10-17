namespace Bit.Core.Billing.Organizations.Models;

public record OrganizationMetadata(
    bool IsOnSecretsManagerStandalone,
    int OrganizationOccupiedSeats)
{
    public static OrganizationMetadata Default => new OrganizationMetadata(
        false,
        0);
}
