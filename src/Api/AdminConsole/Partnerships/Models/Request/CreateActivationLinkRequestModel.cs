using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

namespace Bit.Api.AdminConsole.Partnerships.Models.Request;

/// <summary>
/// Requests an activation link for a provisioned entitlement.
/// </summary>
public class CreateActivationLinkRequestModel
{
    /// <summary>
    /// The partner's identifier for the customer, as provisioned.
    /// </summary>
    [Required]
    [StringLength(OrganizationPartnershipEntitlement.ExternalIdMaxLength)]
    public string ExternalId { get; set; } = null!;

    /// <summary>
    /// Where to send the customer after activation. Must be an absolute https URL on one of the partnership's
    /// registered return origins.
    /// </summary>
    [StringLength(CreatePartnershipActivationLinkRequest.ReturnUrlMaxLength)]
    public string? ReturnUrl { get; set; }
}
