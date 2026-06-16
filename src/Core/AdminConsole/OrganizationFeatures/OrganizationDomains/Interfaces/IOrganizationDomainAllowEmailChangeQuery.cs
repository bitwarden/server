using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IOrganizationDomainAllowEmailChangeQuery
{
    /// <summary>
    /// Throws <see cref="BadRequestException"/> if <paramref name="user"/> is not permitted to
    /// change their email to <paramref name="newEmail"/>. Short-circuits when the new email shares
    /// a domain with the user's current email, since a same-domain change is a no-op against the
    /// policy.
    /// </summary>
    /// <remarks>
    /// If the user's account is claimed by one or more organizations, the new domain must itself
    /// be a verified domain of one of those organizations. Otherwise, the change is allowed unless
    /// the new domain is verified by an organization with the BlockClaimedDomainAccountCreation
    /// policy enabled.
    /// </remarks>
    /// <param name="newEmail">The new email address trying to be set (e.g. "user@example.com").</param>
    Task ValidateAllowedAsync(User user, string newEmail);
}
