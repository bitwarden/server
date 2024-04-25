using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadataResponse From(OrganizationMetadataDTO metadataDTO)
        => new(metadataDTO.IsOnSecretsManagerStandalone);
}
