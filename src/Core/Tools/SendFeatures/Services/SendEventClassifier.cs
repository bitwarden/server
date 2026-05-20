#nullable enable

using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Tools.SendFeatures.Services.Interfaces;

namespace Bit.Core.Tools.SendFeatures.Services;

public class SendEventClassifier : ISendEventClassifier
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public SendEventClassifier(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task<Func<Guid, EventType?>?> BuildAccessResolverAsync(
        Guid sendOwnerUserId,
        string? accessorEmail,
        EventType claimedDomainVariant,
        EventType externalDomainVariant)
    {
        var accessorDomain = ExtractDomain(accessorEmail);
        if (accessorDomain is null)
        {
            return null;
        }

        var domainsByOrg = await LoadClaimedDomainsByOrgAsync(sendOwnerUserId);
        if (domainsByOrg.Count == 0)
        {
            return null;
        }

        return orgId =>
        {
            if (!domainsByOrg.TryGetValue(orgId, out var claimedDomains))
            {
                return null;
            }
            return claimedDomains.Contains(accessorDomain) ? claimedDomainVariant : externalDomainVariant;
        };
    }

    public async Task<Func<Guid, EventType?>?> BuildCreationResolverAsync(
        Guid sendOwnerUserId,
        string? recipientEmails,
        EventType claimedDomainVariant,
        EventType externalDomainVariant)
    {
        if (string.IsNullOrWhiteSpace(recipientEmails))
        {
            return null;
        }

        var recipientDomains = recipientEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ExtractDomain)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (recipientDomains.Count == 0)
        {
            return null;
        }

        var domainsByOrg = await LoadClaimedDomainsByOrgAsync(sendOwnerUserId);
        if (domainsByOrg.Count == 0)
        {
            return null;
        }

        return orgId =>
        {
            if (!domainsByOrg.TryGetValue(orgId, out var claimedDomains))
            {
                return null;
            }
            return recipientDomains.All(claimedDomains.Contains)
                ? claimedDomainVariant
                : externalDomainVariant;
        };
    }

    private async Task<Dictionary<Guid, HashSet<string>>> LoadClaimedDomainsByOrgAsync(Guid userId)
    {
        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(userId);
        var confirmedOrgIds = orgUsers
            .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
            .Select(ou => ou.OrganizationId)
            .Distinct()
            .ToList();

        if (confirmedOrgIds.Count == 0)
        {
            return new Dictionary<Guid, HashSet<string>>();
        }

        var domains = await _organizationDomainRepository
            .GetVerifiedDomainsByOrganizationIdsAsync(confirmedOrgIds);

        return domains
            .GroupBy(d => d.OrganizationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => d.DomainName.ToLowerInvariant())
                      .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static string? ExtractDomain(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var atIdx = trimmed.IndexOf('@');
        if (atIdx < 0 || atIdx == trimmed.Length - 1)
        {
            return null;
        }

        return trimmed[(atIdx + 1)..].ToLowerInvariant();
    }
}
