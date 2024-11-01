using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadataResponse From(OrganizationMetadata metadata)
        => new(
            metadata.IsEligibleForSelfHost,
            metadata.IsManaged,
            metadata.IsOnSecretsManagerStandalone);
}
