using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IOrganizationDomainAllowEmailChangeQuery
{
    /// <summary>
    /// Determines whether <paramref name="user"/> is permitted to change their email to an address
    /// in <paramref name="newEmailDomain"/>.
    /// </summary>
    /// <remarks>
    /// If the user's account is claimed by one or more organizations, the new domain must itself be
    /// a verified domain of one of those organizations. Otherwise, the change is allowed unless the
    /// new domain is verified by an organization with the BlockClaimedDomainAccountCreation policy enabled.
    /// </remarks>
    /// <param name="newEmailDomain">The bare domain portion of the target email (e.g. "example.com").</param>
    /// <returns><c>true</c> if the email change is permitted; otherwise <c>false</c>.</returns>
    Task<bool> IsAllowedAsync(User user, string newEmailDomain);
}
