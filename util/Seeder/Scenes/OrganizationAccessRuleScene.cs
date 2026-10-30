using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Pam.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates a PAM access rule in an organization and makes it govern the given collections. With no conditions
/// the rule auto-approves requests.
/// </summary>
public class OrganizationAccessRuleScene(
    IOrganizationRepository organizationRepository,
    ICollectionRepository collectionRepository,
    IAccessRuleRepository accessRuleRepository,
    IManglerService manglerService) : IScene<OrganizationAccessRuleScene.Request, OrganizationAccessRuleScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool Enabled { get; set; } = true;
        /// <summary>Collections the rule governs. Must belong to the organization.</summary>
        public IEnumerable<Guid>? CollectionIds { get; set; }
        /// <summary>Adds an <c>ip_allowlist</c> condition with these CIDRs.</summary>
        public IReadOnlyList<string>? IpAllowlistCidrs { get; set; }
        /// <summary>Adds a <c>human_approval</c> condition.</summary>
        public bool RequireHumanApproval { get; set; }
        public bool SingleActiveLease { get; set; }
        public int? DefaultLeaseDurationSeconds { get; set; }
        public int? MaxLeaseDurationSeconds { get; set; }
        public bool AllowsExtensions { get; set; }
        /// <summary>Required and positive when <see cref="AllowsExtensions"/> is true.</summary>
        public int? MaxExtensionDurationSeconds { get; set; }
    }

    public class Result
    {
        public required Guid AccessRuleId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var collectionIds = request.CollectionIds?.Distinct().ToList() ?? [];
        if (collectionIds.Count > 0)
        {
            var found = await collectionRepository.GetManyByManyIdsAsync(collectionIds);
            var foundIds = found.Where(c => c.OrganizationId == organization.Id).Select(c => c.Id).ToHashSet();
            var missing = collectionIds.Where(id => !foundIds.Contains(id)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Collections not found in organization {organization.Id}: {string.Join(", ", missing)}.");
            }

            var governed = found.Where(c => c.AccessRuleId != null).Select(c => c.Id).ToList();
            if (governed.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Collections already governed by an access rule: {string.Join(", ", governed)}.");
            }
        }

        var accessRule = AccessRuleSeeder.Create(
            organization.Id,
            request.Name,
            request.IpAllowlistCidrs,
            request.RequireHumanApproval,
            request.Description,
            request.Enabled,
            request.SingleActiveLease,
            request.DefaultLeaseDurationSeconds,
            request.MaxLeaseDurationSeconds,
            request.AllowsExtensions,
            request.MaxExtensionDurationSeconds);

        await accessRuleRepository.CreateAsync(accessRule);

        if (collectionIds.Count > 0)
        {
            await collectionRepository.SetAccessRuleAssociationsAsync(
                organization.Id, accessRule.Id, collectionIds, []);
        }

        return new SceneResult<Result>(
            result: new Result { AccessRuleId = accessRule.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
