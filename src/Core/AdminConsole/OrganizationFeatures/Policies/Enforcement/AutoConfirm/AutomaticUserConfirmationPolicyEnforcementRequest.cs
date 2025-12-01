using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementQuery"/>
/// </summary>
public record AutomaticUserConfirmationPolicyEnforcementRequest
{
    /// <summary>
    /// Organization user to be confirmed to be confirmed
    /// </summary>
    public OrganizationUser OrganizationUser { get; }
    /// <summary>
    /// Collection of organization users that match the provided user. This must be populated with organizations users associated with the
    /// organization user to confirm.
    /// </summary>
    public IEnumerable<OrganizationUser> OtherOrganizationsOrganizationUsers { get; }
    /// <summary>
    /// User associated with the organization user to be confirmed
    /// </summary>
    public User User { get; }

    /// <summary>
    /// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementQuery"/>.
    /// </summary>
    /// <remarks>
    /// This record is used to encapsulate the data required for handling the automatic confirmation policy enforcement.
    ///
    /// </remarks>
    /// <param name="organizationUserToValidate">The organization user to be validated within the current organization context.</param>
    /// <param name="organizationUsersForOtherOrganizations">THIS MUST BE POPULATED CORRECTLY. A collection of organization user records that match the provided user.</param>
    /// <param name="user">The general user associated with the operation.</param>
    public AutomaticUserConfirmationPolicyEnforcementRequest(
        OrganizationUser organizationUserToValidate,
        IEnumerable<OrganizationUser> organizationUsersForOtherOrganizations,
        User user)
    {
        OrganizationUser = organizationUserToValidate;
        OtherOrganizationsOrganizationUsers = organizationUsersForOtherOrganizations;
        User = user;
    }
}

