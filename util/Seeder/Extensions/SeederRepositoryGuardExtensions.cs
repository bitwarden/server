using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Seeder.Extensions;

internal static class SeederRepositoryGuardExtensions
{
    public static async Task<Organization> GetSecretsManagerOrganizationOrThrowAsync(
        this IOrganizationRepository organizationRepository, Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {organizationId} not found.");
        }

        if (!organization.UseSecretsManager)
        {
            throw new InvalidOperationException(
                $"Organization {organizationId} does not have Secrets Manager enabled.");
        }

        return organization;
    }

    public static async Task ThrowIfProjectsNotInOrganizationAsync(
        this IProjectRepository projectRepository, IEnumerable<Guid>? projectIds, Guid organizationId)
    {
        var ids = projectIds?.ToList();
        if (ids is not { Count: > 0 })
        {
            return;
        }

        if (!await projectRepository.ProjectsAreInOrganization(ids, organizationId))
        {
            throw new InvalidOperationException(
                $"One or more projects are not in organization {organizationId}.");
        }
    }

    public static async Task ThrowIfServiceAccountsNotInOrganizationAsync(
        this IServiceAccountRepository serviceAccountRepository, IEnumerable<Guid>? serviceAccountIds, Guid organizationId)
    {
        var ids = serviceAccountIds?.ToList();
        if (ids is not { Count: > 0 })
        {
            return;
        }

        if (!await serviceAccountRepository.ServiceAccountsAreInOrganizationAsync(ids, organizationId))
        {
            throw new InvalidOperationException(
                $"One or more service accounts are not in organization {organizationId}.");
        }
    }

    public static async Task ThrowIfGroupsNotInOrganizationAsync(
        this IGroupRepository groupRepository, IEnumerable<Guid>? groupIds, Guid organizationId)
    {
        var ids = groupIds?.ToList();
        if (ids is not { Count: > 0 })
        {
            return;
        }

        var groups = await groupRepository.GetManyByManyIds(ids);
        if (!AllResolvedInOrganization(ids, groups.Select(g => (g.Id, g.OrganizationId)), organizationId))
        {
            throw new InvalidOperationException(
                $"One or more groups are not in organization {organizationId}.");
        }
    }

    public static async Task ThrowIfOrganizationUsersNotInOrganizationAsync(
        this IOrganizationUserRepository organizationUserRepository, IEnumerable<Guid>? organizationUserIds, Guid organizationId)
    {
        var ids = organizationUserIds?.ToList();
        if (ids is not { Count: > 0 })
        {
            return;
        }

        var users = await organizationUserRepository.GetManyAsync(ids);
        if (!AllResolvedInOrganization(ids, users.Select(u => (u.Id, u.OrganizationId)), organizationId))
        {
            throw new InvalidOperationException(
                $"One or more organization users are not in organization {organizationId}.");
        }
    }

    private static bool AllResolvedInOrganization(
        IReadOnlyCollection<Guid> requestedIds,
        IEnumerable<(Guid Id, Guid OrganizationId)> resolved,
        Guid organizationId)
    {
        var matched = resolved
            .Where(r => r.OrganizationId == organizationId)
            .Select(r => r.Id)
            .ToHashSet();

        return requestedIds.All(matched.Contains);
    }
}
