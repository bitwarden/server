using Bit.Core.Billing.Organizations.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsOnSecretsManagerStandalone,
    int OrganizationOccupiedSeats)
{
    public static OrganizationMetadataResponse From(OrganizationMetadata metadata)
        => new(
            metadata.IsOnSecretsManagerStandalone,
            metadata.OrganizationOccupiedSeats);
}
