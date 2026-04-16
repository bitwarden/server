using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class CreateOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    TimeProvider timeProvider)
    : ICreateOrganizationInviteLinkCommand
{
    private static readonly DomainNameValidatorAttribute _domainValidator = new();

    public async Task<CommandResult<OrganizationInviteLink>> CreateAsync(
        CreateOrganizationInviteLinkRequest request)
    {
        var sanitizedDomains = SanitizeDomains(request.AllowedDomains);
        if (sanitizedDomains.Count == 0)
        {
            return new InviteLinkDomainsRequired();
        }

        var invalidDomains = sanitizedDomains.Where(d => !IsValidDomain(d)).ToList();
        if (invalidDomains.Count > 0)
        {
            return new InviteLinkInvalidDomains(invalidDomains);
        }

        var existingLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        if (existingLink != null)
        {
            return new InviteLinkAlreadyExists();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = request.OrganizationId,
            EncryptedInviteKey = request.EncryptedInviteKey,
            EncryptedOrgKey = request.EncryptedOrgKey,
            CreationDate = now,
            RevisionDate = now,
        };
        inviteLink.SetAllowedDomains(sanitizedDomains);
        inviteLink.SetNewId();

        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return inviteLink;
    }

    /// <summary>
    /// Sanitizes the domains by trimming whitespace and removing empty domains.
    /// </summary>
    /// <param name="domains">The domains to sanitize.</param>
    /// <returns>A list of sanitized domains.</returns>
    private static List<string> SanitizeDomains(IEnumerable<string>? domains) =>
        domains?
            .Select(d => d?.Trim())
            .Where(d => !string.IsNullOrEmpty(d))
            .Cast<string>()
            .ToList() ?? [];

    private static bool IsValidDomain(string domain) => _domainValidator.IsValid(domain);
}
