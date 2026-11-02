using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Pam.Repositories;
using Bit.Seeder.Extensions;
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
        public IEnumerable<Guid>? CollectionIds { get; set; }
        public IEnumerable<string>? IpAllowlistCidrs { get; set; }
        public bool RequireHumanApproval { get; set; }
        public bool SingleActiveLease { get; set; }
        public int? DefaultLeaseDurationSeconds { get; set; }
        public int? MaxLeaseDurationSeconds { get; set; }
        public bool AllowsExtensions { get; set; }
        public int? MaxExtensionDurationSeconds { get; set; }
    }

    public class Result
    {
        public required Guid AccessRuleId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var collections = await organizationRepository.GetDistinctCollectionsOrThrowAsync(
            collectionRepository, request.OrganizationId, request.CollectionIds ?? []);
        collections.ThrowIfCollectionsGovernedByAccessRule();
        var collectionIds = collections.Select(c => c.Id).ToList();

        var accessRule = AccessRuleSeeder.Create(
            request.OrganizationId,
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
                request.OrganizationId, accessRule.Id, collectionIds, []);
        }

        return new SceneResult<Result>(
            result: new Result { AccessRuleId = accessRule.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
