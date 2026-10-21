using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Vault.Repositories;

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

    public static Task ThrowIfProjectsNotInOrganizationAsync(
        this IProjectRepository projectRepository, IEnumerable<Guid>? projectIds, Guid organizationId)
        => ThrowIfAnyNotOwnedAsync(
            projectIds,
            ids => projectRepository.ProjectsAreInOrganization(ids, organizationId),
            $"One or more projects are not in organization {organizationId}.");

    public static Task ThrowIfServiceAccountsNotInOrganizationAsync(
        this IServiceAccountRepository serviceAccountRepository, IEnumerable<Guid>? serviceAccountIds, Guid organizationId)
        => ThrowIfAnyNotOwnedAsync(
            serviceAccountIds,
            ids => serviceAccountRepository.ServiceAccountsAreInOrganizationAsync(ids, organizationId),
            $"One or more service accounts are not in organization {organizationId}.");

    public static Task ThrowIfGroupsNotInOrganizationAsync(
        this IGroupRepository groupRepository, IEnumerable<Guid>? groupIds, Guid organizationId)
        => ThrowIfAnyNotOwnedAsync(
            groupIds,
            async ids => AllResolvedToOwner(
                ids,
                (await groupRepository.GetManyByManyIds(ids)).Select(g => (g.Id, g.OrganizationId)),
                organizationId),
            $"One or more groups are not in organization {organizationId}.");

    public static Task ThrowIfOrganizationUsersNotInOrganizationAsync(
        this IOrganizationUserRepository organizationUserRepository, IEnumerable<Guid>? organizationUserIds, Guid organizationId)
        => ThrowIfAnyNotOwnedAsync(
            organizationUserIds,
            async ids => AllResolvedToOwner(
                ids,
                (await organizationUserRepository.GetManyAsync(ids)).Select(u => (u.Id, u.OrganizationId)),
                organizationId),
            $"One or more organization users are not in organization {organizationId}.");

    public static Task ThrowIfCollectionsNotInOrganizationAsync(
        this ICollectionRepository collectionRepository, IEnumerable<Guid>? collectionIds, Guid organizationId)
        => ThrowIfAnyNotOwnedAsync(
            collectionIds,
            async ids => AllResolvedToOwner(
                ids,
                (await collectionRepository.GetManyByManyIdsAsync(ids)).Select(c => (c.Id, c.OrganizationId)),
                organizationId),
            $"One or more collections are not in organization {organizationId}.");

    public static Task ThrowIfUsersNotConfirmedMembersOfOrganizationAsync(
        this IOrganizationUserRepository organizationUserRepository, IEnumerable<Guid>? userIds, Guid organizationId)
        => ThrowIfAnyNotOwnedAsync(
            userIds,
            async ids => AllResolvedToOwner(
                ids,
                (await organizationUserRepository.GetManyByManyUsersAsync(ids))
                    .Where(ou => ou.UserId.HasValue && ou.Status == OrganizationUserStatusType.Confirmed)
                    .Select(ou => (ou.UserId!.Value, ou.OrganizationId)),
                organizationId),
            $"One or more users are not confirmed members of organization {organizationId}.");

    public static Task ThrowIfFoldersNotOwnedByUserAsync(
        this IFolderRepository folderRepository, IEnumerable<Guid>? folderIds, Guid userId)
        => ThrowIfAnyNotOwnedAsync(
            folderIds,
            async ids => AllResolvedToOwner(
                ids,
                (await folderRepository.GetManyByUserIdAsync(userId)).Select(f => (f.Id, f.UserId)),
                userId),
            $"One or more folders are not owned by user {userId}.");

    public static Task ThrowIfOrganizationsLinkedToProviderAsync(
        this IProviderOrganizationRepository providerOrganizationRepository, IEnumerable<Guid>? organizationIds)
        => ThrowIfAnyNotOwnedAsync(
            organizationIds,
            async ids => await providerOrganizationRepository.GetCountByOrganizationIdsAsync(ids) == 0,
            "Organization already belongs to a provider.");

    private static async Task ThrowIfAnyNotOwnedAsync(
        IEnumerable<Guid>? ids, Func<List<Guid>, Task<bool>> allOwnedAsync, string message)
    {
        var distinctIds = ids?.Distinct().ToList();
        if (distinctIds is not { Count: > 0 })
        {
            return;
        }

        if (!await allOwnedAsync(distinctIds))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool AllResolvedToOwner(
        IReadOnlyCollection<Guid> requestedIds,
        IEnumerable<(Guid Id, Guid OwnerId)> resolved,
        Guid ownerId)
    {
        var matched = resolved
            .Where(r => r.OwnerId == ownerId)
            .Select(r => r.Id)
            .ToHashSet();

        return requestedIds.All(matched.Contains);
    }
}
