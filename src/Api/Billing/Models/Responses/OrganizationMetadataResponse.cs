using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone,
    bool IsSubscriptionUnpaid,
    bool HasSubscription
)
{
    public static OrganizationMetadataResponse From(OrganizationMetadata metadata) =>
        new(
            metadata.IsEligibleForSelfHost,
            metadata.IsManaged,
            metadata.IsOnSecretsManagerStandalone,
            metadata.IsSubscriptionUnpaid,
            metadata.HasSubscription
        );
}
