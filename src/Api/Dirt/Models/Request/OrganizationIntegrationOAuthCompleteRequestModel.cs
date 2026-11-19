using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Dirt.Models.Request;

/// <summary>
/// The <c>code</c> and <c>state</c> the provider returned to the OAuth callback, forwarded by the web vault so an
/// authenticated Organization Owner finishes the integration setup.
/// </summary>
public class OrganizationIntegrationOAuthCompleteRequestModel
{
    [Required]
    public required string Code { get; init; }

    [Required]
    public required string State { get; init; }
}
