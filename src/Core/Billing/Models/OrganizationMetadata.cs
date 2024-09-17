namespace Bit.Core.Billing.Models;

public record OrganizationMetadata(
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadata Default() => new(
        IsOnSecretsManagerStandalone: default);
}
