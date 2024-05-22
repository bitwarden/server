using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses.Organizations;

public record OrganizationMetadataResponse(
    bool IsOnSecretsManagerStandalone)
{
    public static OrganizationMetadataResponse From(OrganizationMetadataDTO metadataDTO)
        => new(metadataDTO.IsOnSecretsManagerStandalone);
}
