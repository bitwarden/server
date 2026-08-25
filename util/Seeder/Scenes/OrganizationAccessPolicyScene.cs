using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Grants Secrets Manager access policies: each grant links a grantee (organization user, group, or
/// service account) to a grantable resource (project or service account) with read/write permissions.
/// </summary>
public class OrganizationAccessPolicyScene(
    IOrganizationRepository organizationRepository,
    IAccessPolicyRepository accessPolicyRepository,
    IManglerService manglerService) : IScene<OrganizationAccessPolicyScene.Request, OrganizationAccessPolicyScene.Result>
{
    public enum GranteeType
    {
        OrganizationUser,
        Group,
        ServiceAccount
    }

    public enum GrantableType
    {
        Project,
        ServiceAccount
    }

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
        public required GranteeType GranteeType { get; set; }
        [Required]
        public required Guid GranteeId { get; set; }
        [Required]
        public required GrantableType GrantableType { get; set; }
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
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        if (!organization.UseSecretsManager)
        {
            throw new InvalidOperationException(
                $"Organization {request.OrganizationId} does not have Secrets Manager enabled.");
        }

        var policies = request.Grants.Select(BuildPolicy).ToList();

        var created = await accessPolicyRepository.CreateManyAsync(policies);

        return new SceneResult<Result>(
            result: new Result
            {
                Count = created.Count,
                AccessPolicyIds = created.Select(p => p.Id).ToList()
            },
            mangleMap: manglerService.GetMangleMap());
    }

    private static BaseAccessPolicy BuildPolicy(Grant grant) =>
        (grant.GranteeType, grant.GrantableType) switch
        {
            (GranteeType.OrganizationUser, GrantableType.Project) => new UserProjectAccessPolicy
            {
                OrganizationUserId = grant.GranteeId,
                GrantedProjectId = grant.GrantableId,
                Read = grant.Read,
                Write = grant.Write
            },
            (GranteeType.OrganizationUser, GrantableType.ServiceAccount) => new UserServiceAccountAccessPolicy
            {
                OrganizationUserId = grant.GranteeId,
                GrantedServiceAccountId = grant.GrantableId,
                Read = grant.Read,
                Write = grant.Write
            },
            (GranteeType.Group, GrantableType.Project) => new GroupProjectAccessPolicy
            {
                GroupId = grant.GranteeId,
                GrantedProjectId = grant.GrantableId,
                Read = grant.Read,
                Write = grant.Write
            },
            (GranteeType.Group, GrantableType.ServiceAccount) => new GroupServiceAccountAccessPolicy
            {
                GroupId = grant.GranteeId,
                GrantedServiceAccountId = grant.GrantableId,
                Read = grant.Read,
                Write = grant.Write
            },
            (GranteeType.ServiceAccount, GrantableType.Project) => new ServiceAccountProjectAccessPolicy
            {
                ServiceAccountId = grant.GranteeId,
                GrantedProjectId = grant.GrantableId,
                Read = grant.Read,
                Write = grant.Write
            },
            _ => throw new InvalidOperationException(
                $"Unsupported access policy: {grant.GranteeType} granted to {grant.GrantableType}.")
        };
}
