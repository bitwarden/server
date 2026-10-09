using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class CreateOrganizationScopedApiKeyRequestModel : SecretVerificationRequestModel
{
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// Scopes from the organization API key scope catalog, for example <c>api.organization.events.read</c>.
    /// </summary>
    [Required]
    public required IEnumerable<string> Scopes { get; set; }

    public DateTime? ExpireAt { get; set; }

    public CreateOrganizationScopedApiKeyRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        Scopes = Scopes,
        ExpireAt = ExpireAt,
    };
}
