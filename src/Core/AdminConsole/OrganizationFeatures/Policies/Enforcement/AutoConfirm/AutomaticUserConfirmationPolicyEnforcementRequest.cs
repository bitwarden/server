using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Request object for <see cref="AutomaticUserConfirmationPolicyEnforcementValidator"/>
/// </summary>
public record AutomaticUserConfirmationPolicyEnforcementRequest
{
    /// <summary>
    /// Organization to be validated
    /// </summary>
    public Guid OrganizationId { get; }

    /// <summary>
    /// All organization users that match the provided user.
    /// </summary>
    public ICollection<OrganizationUser> AllOrganizationUsers { get; }

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
    /// <param name="organizationId">The organization to be validated.</param>
    /// <param name="organizationUsers">All organization users that match the provided user.</param>
    /// <param name="user">The user entity connecting all org users provided.</param>
    public AutomaticUserConfirmationPolicyEnforcementRequest(
        Guid organizationId,
        IEnumerable<OrganizationUser> organizationUsers,
        User user)
    {
        OrganizationId = organizationId;
        AllOrganizationUsers = organizationUsers.ToArray();
        User = user;
    }
}

