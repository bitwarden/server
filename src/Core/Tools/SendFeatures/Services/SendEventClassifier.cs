#nullable enable

using Bit.Core.Enums;
using Bit.Core.Models.Data;
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

    private static readonly IReadOnlyDictionary<Guid, SendAccessEventOrgContext> _empty =
        new Dictionary<Guid, SendAccessEventOrgContext>();

    public async Task<IReadOnlyDictionary<Guid, SendAccessEventOrgContext>> BuildAccessContextAsync(
        Guid sendOwnerUserId,
        string? accessorEmail)
    {
        var accessorDomain = ExtractDomain(accessorEmail);
        if (accessorDomain is null)
        {
            // Anonymous / password / no-email access: the accessor is not identifiable, so every org
            // renders as External.
            return _empty;
        }

        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(sendOwnerUserId);
        var confirmedOrgIds = orgUsers
            .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
            .Select(ou => ou.OrganizationId)
            .Distinct()
            .ToList();

        if (confirmedOrgIds.Count == 0)
        {
            return _empty;
        }

        var claimedDomainsByOrg = await LoadClaimedDomainsByOrgAsync(confirmedOrgIds);

        // Resolve the accessor's membership in every org concurrently. Each repository call uses its
        // own connection/DbContext scope
        var membershipByOrg = confirmedOrgIds.ToDictionary(
            orgId => orgId,
            orgId => _organizationUserRepository.GetByOrganizationEmailAsync(orgId, accessorEmail!));
        await Task.WhenAll(membershipByOrg.Values);

        var context = new Dictionary<Guid, SendAccessEventOrgContext>();
        foreach (var orgId in confirmedOrgIds)
        {
            // Accessor is a confirmed member of this org: attribute to their platform user (the id the
            // Admin Console member list is keyed on).
            var accessor = await membershipByOrg[orgId];
            if (accessor is { Status: OrganizationUserStatusType.Confirmed, UserId: not null })
            {
                context[orgId] = new SendAccessEventOrgContext(accessor.UserId.Value, null);
                continue;
            }

            // Accessor's email domain is a claimed domain of this org: record the domain only.
            if (claimedDomainsByOrg.TryGetValue(orgId, out var claimedDomains)
                && claimedDomains.Contains(accessorDomain))
            {
                context[orgId] = new SendAccessEventOrgContext(null, accessorDomain);
            }

            // Otherwise the org has no entry and the access renders as External.
        }

        return context;
    }

    private async Task<Dictionary<Guid, HashSet<string>>> LoadClaimedDomainsByOrgAsync(IEnumerable<Guid> confirmedOrgIds)
    {
        var domains = await _organizationDomainRepository
            .GetVerifiedDomainsByOrganizationIdsAsync(confirmedOrgIds);

        return domains
            .GroupBy(d => d.OrganizationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => d.DomainName).ToHashSet(StringComparer.OrdinalIgnoreCase));
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
