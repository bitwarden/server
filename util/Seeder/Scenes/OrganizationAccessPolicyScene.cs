using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Grants Secrets Manager access policies: each grant links a grantee (organization user, group, or
/// service account) to a grantable resource (project or service account) with read/write permissions.
/// </summary>
public class OrganizationAccessPolicyScene(
    IOrganizationRepository organizationRepository,
    IAccessPolicyRepository accessPolicyRepository,
    IProjectRepository projectRepository,
    IServiceAccountRepository serviceAccountRepository,
    IGroupRepository groupRepository,
    IOrganizationUserRepository organizationUserRepository,
    IManglerService manglerService) : IScene<OrganizationAccessPolicyScene.Request, OrganizationAccessPolicyScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        [MinLength(1)]
        public required IEnumerable<Grant> Grants { get; set; }
    }

    public class Grant
    {
        [Required]
        public required AccessPolicySeeder.GranteeType GranteeType { get; set; }
        [Required]
        public required Guid GranteeId { get; set; }
        [Required]
        public required AccessPolicySeeder.GrantableType GrantableType { get; set; }
        [Required]
        public required Guid GrantableId { get; set; }
        public bool Read { get; set; } = true;
        public bool Write { get; set; }
    }

    public class Result
    {
        public required int Count { get; init; }
        public required IEnumerable<Guid> AccessPolicyIds { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        await organizationRepository.GetSecretsManagerOrganizationOrThrowAsync(request.OrganizationId);

        var grants = request.Grants.ToList();

        var projectIds = GrantableIds(grants, AccessPolicySeeder.GrantableType.Project);
        var serviceAccountIds = GrantableIds(grants, AccessPolicySeeder.GrantableType.ServiceAccount)
            .Concat(GranteeIds(grants, AccessPolicySeeder.GranteeType.ServiceAccount))
            .Distinct();
        var groupIds = GranteeIds(grants, AccessPolicySeeder.GranteeType.Group);
        var organizationUserIds = GranteeIds(grants, AccessPolicySeeder.GranteeType.OrganizationUser);

        await projectRepository.ThrowIfProjectsNotInOrganizationAsync(projectIds, request.OrganizationId);
        await serviceAccountRepository.ThrowIfServiceAccountsNotInOrganizationAsync(serviceAccountIds, request.OrganizationId);
        await groupRepository.ThrowIfGroupsNotInOrganizationAsync(groupIds, request.OrganizationId);
        await organizationUserRepository.ThrowIfOrganizationUsersNotInOrganizationAsync(organizationUserIds, request.OrganizationId);

        var policies = grants
            .Select(g => AccessPolicySeeder.Create(g.GranteeType, g.GranteeId, g.GrantableType, g.GrantableId, g.Read, g.Write))
            .ToList();

        var created = await accessPolicyRepository.CreateManyAsync(policies);

        return new SceneResult<Result>(
            result: new Result
            {
                Count = created.Count,
                AccessPolicyIds = created.Select(p => p.Id).ToList()
            },
            mangleMap: manglerService.GetMangleMap());
    }

    private static IEnumerable<Guid> GrantableIds(IEnumerable<Grant> grants, AccessPolicySeeder.GrantableType type) =>
        grants.Where(g => g.GrantableType == type).Select(g => g.GrantableId).Distinct();

    private static IEnumerable<Guid> GranteeIds(IEnumerable<Grant> grants, AccessPolicySeeder.GranteeType type) =>
        grants.Where(g => g.GranteeType == type).Select(g => g.GranteeId).Distinct();
}
