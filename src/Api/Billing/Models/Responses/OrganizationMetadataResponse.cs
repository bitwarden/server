using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsEligibleForSelfHost,
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadataResponse From(OrganizationMetadata metadata)
        => new(
            metadata.IsEligibleForSelfHost,
            metadata.IsOnSecretsManagerStandalone);
}
