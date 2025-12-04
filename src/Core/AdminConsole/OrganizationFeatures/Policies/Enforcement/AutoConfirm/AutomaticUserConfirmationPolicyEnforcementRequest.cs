using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementValidator"/>
/// </summary>
public record AutomaticUserConfirmationPolicyEnforcementRequest
{
    /// <summary>
    /// Organization user to be validated
    /// </summary>
    public Guid OrganizationUserId { get; }

    /// <summary>
    /// All organization users that match the provided user.
    /// </summary>
    public IEnumerable<OrganizationUser> AllOrganizationUsers { get; }
    /// <summary>
    /// User associated with the organization user to be confirmed
    /// </summary>
    public User User { get; }

    /// <summary>
    /// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementValidator"/>.
    /// </summary>
    /// <remarks>
    /// This record is used to encapsulate the data required for handling the automatic confirmation policy enforcement.
    /// </remarks>
    /// <param name="organizationUserId">The organization user id to be validated.</param>
    /// <param name="organizationUsers">All organization users that match the provided user.</param>
    /// <param name="user">The general user associated with the operation.</param>
    public AutomaticUserConfirmationPolicyEnforcementRequest(
        Guid organizationUserId,
        IEnumerable<OrganizationUser> organizationUsers,
        User user)
    {
        OrganizationUserId = organizationUserId;
        AllOrganizationUsers = organizationUsers;
        User = user;
    }
}

