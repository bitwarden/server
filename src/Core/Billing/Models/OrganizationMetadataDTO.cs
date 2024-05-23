namespace Bit.Core.Billing.Models;

public record OrganizationMetadataDTO(
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadataDTO Default() => new(
        IsOnSecretsManagerStandalone: default);
}
