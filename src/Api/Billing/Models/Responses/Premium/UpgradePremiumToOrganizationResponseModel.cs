namespace Bit.Api.Billing.Models.Responses.Premium;

/// <summary>
/// Response model containing the ID of the organization created by upgrading a Premium subscription.
/// </summary>
public class UpgradePremiumToOrganizationResponseModel(Guid organizationId)
{
    /// <summary>
    /// The ID of the newly created organization.
    /// </summary>
    public Guid OrganizationId { get; init; } = organizationId;
}
