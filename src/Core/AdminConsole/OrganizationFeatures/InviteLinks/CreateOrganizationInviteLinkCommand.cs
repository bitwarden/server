using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class CreateOrganizationInviteLinkCommand(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IApplicationCacheService applicationCacheService,
    TimeProvider timeProvider)
    : ICreateOrganizationInviteLinkCommand
{
    private static readonly DomainNameValidatorAttribute _domainValidator = new();

    public async Task<CommandResult<OrganizationInviteLink>> CreateAsync(
        CreateOrganizationInviteLinkRequest request)
    {
        if (!await OrganizationHasInviteLinksAbilityAsync(request.OrganizationId))
        {
            return new InviteLinkNotAvailable();
        }

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

    private async Task<bool> OrganizationHasInviteLinksAbilityAsync(Guid organizationId)
    {
        var ability = await applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return ability is not null && ability.UseInviteLinks;
    }

    /// <summary>
    /// Sanitizes the domains by trimming whitespace and removing empty domains.
    /// </summary>
    /// <param name="domains">The domains to sanitize.</param>
    /// <returns>A list of sanitized domains.</returns>
    private static List<string> SanitizeDomains(IEnumerable<string>? domains) =>
        domains?
            .Select(d => d?.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrEmpty(d))
            .Cast<string>()
            .ToList() ?? [];

    private static bool IsValidDomain(string domain) => _domainValidator.IsValid(domain);
}
