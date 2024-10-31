namespace Bit.Core.Billing.Models;

public record OrganizationMetadata(
    bool IsEligibleForSelfHost,
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadata Default() => new(
        IsEligibleForSelfHost: false,
        IsOnSecretsManagerStandalone: false);
}
