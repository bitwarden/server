using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementQuery"/>
/// </summary>
public record AutomaticUserConfirmationPolicyEnforcementRequest
{
    public OrganizationUser OrganizationUser { get; }
    public IEnumerable<OrganizationUser>? OtherOrganizationsOrganizationUsers { get; }
    public User User { get; }

    /// <summary>
    /// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementQuery"/>.
    /// </summary>
    /// <remarks>
    /// This record is used to encapsulate the data required for handling the automatic confirmation policy enforcement.
    ///
    /// Use if you've retrieved the organization user records for other organizations already.
    /// </remarks>
    /// <param name="organizationUserToValidate">The organization user to be validated within the current organization context.</param>
    /// <param name="organizationUsersForOtherOrganizations">A collection of organization user records that match the provided user.</param>
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

    /// <summary>
    /// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementQuery"/>
    /// </summary>
    /// <remarks>
    /// Use this constructor when you haven't retrieved the organization user records for other organizations yet.
    /// </remarks>
    /// <param name="organizationUserToValidate">Organization User to Validate</param>
    /// <param name="user">User record for orgUser</param>
    public AutomaticUserConfirmationPolicyEnforcementRequest(OrganizationUser organizationUserToValidate,
        User user)
    {
        OrganizationUser = organizationUserToValidate;
        OtherOrganizationsOrganizationUsers = null;
        User = user;
    }

    public void Deconstruct(out OrganizationUser organizationUser, out ICollection<OrganizationUser>? otherOrganizationsOrganizationUsers, out User user)
    {
        organizationUser = OrganizationUser;
        otherOrganizationsOrganizationUsers = OtherOrganizationsOrganizationUsers?.ToArray();
        user = User;
    }
}

