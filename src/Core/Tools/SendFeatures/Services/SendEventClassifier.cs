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
    private readonly IUserRepository _userRepository;

    public SendEventClassifier(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository,
        IUserRepository userRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationDomainRepository = organizationDomainRepository;
        _userRepository = userRepository;
    }

    private static readonly IReadOnlyDictionary<Guid, SendAccessEventOrgContext> _empty =
        new Dictionary<Guid, SendAccessEventOrgContext>();

    public async Task<IReadOnlyDictionary<Guid, SendAccessEventOrgContext>> BuildAccessContextAsync(
        Guid sendOwnerUserId,
        string? accessorEmail)
    {
        // Normalize the accessor email once, up front, so resolution is correct regardless of the
        // caller. GetByEmailAsync is case-sensitive on some providers (e.g. SQLite), so a non-normalized
        // email would silently fail to match a confirmed member and render as External.
        var normalizedEmail = accessorEmail?.Trim().ToLowerInvariant();

        var accessorDomain = ExtractDomain(normalizedEmail);
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

        // Resolve the accessor as a confirmed member via their linked user account. OrganizationUser.Email
        // is only populated for pending invites, so a confirmed member can only be matched through the User
        // record (email -> user -> confirmed memberships), never via OrganizationUser.Email.
        Guid? accessorUserId = null;
        var accessorMemberOrgIds = new HashSet<Guid>();
        var accessorUser = await _userRepository.GetByEmailAsync(normalizedEmail!);
        if (accessorUser is not null)
        {
            accessorUserId = accessorUser.Id;
            var accessorMemberships = await _organizationUserRepository.GetManyByUserAsync(accessorUser.Id);
            foreach (var membership in accessorMemberships
                .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed))
            {
                accessorMemberOrgIds.Add(membership.OrganizationId);
            }
        }

        var context = new Dictionary<Guid, SendAccessEventOrgContext>();
        foreach (var orgId in confirmedOrgIds)
        {
            // Accessor is a confirmed member of this org: attribute to their platform user (the id the
            // Admin Console member list is keyed on).
            if (accessorUserId.HasValue && accessorMemberOrgIds.Contains(orgId))
            {
                context[orgId] = new SendAccessEventOrgContext(accessorUserId.Value, null);
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

    // Returns the domain part of an already-normalized (trimmed + lowercased) email, or null when the
    // value is absent or malformed. BuildAccessContextAsync normalizes before calling, so this neither
    // trims nor lowercases.
    private static string? ExtractDomain(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        var atIdx = email.IndexOf('@');
        if (atIdx < 0 || atIdx == email.Length - 1)
        {
            return null;
        }

        return email[(atIdx + 1)..];
    }
}
