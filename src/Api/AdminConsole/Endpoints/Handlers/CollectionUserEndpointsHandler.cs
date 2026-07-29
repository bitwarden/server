using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Utilities;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Endpoints.Handlers;

public class CollectionUserEndpointsHandler(
    ICollectionRepository collectionRepository,
    IAuthorizationService authorizationService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ICurrentContext currentContext,
    IModifyCollectionUserAccessCommand modifyCollectionUserAccessCommand)
{
    /// <summary>
    /// Applies an add/update/remove delta to one or more collections' user access.
    /// </summary>
    public async Task<IResult> PatchUserAccessAsync(
        Guid orgId, IReadOnlyCollection<Guid> collectionIds,
        IEnumerable<SelectionReadOnlyRequestModel> add, IEnumerable<SelectionReadOnlyRequestModel> update,
        IEnumerable<Guid> remove, ClaimsPrincipal user)
    {
        var addIds = add.Select(a => a.Id).ToHashSet();
        var updateIds = update.Select(u => u.Id).ToHashSet();
        var removeIds = remove.ToHashSet();

        // A single collection is resolved directly instead of pulling the whole organization's access graph.
        List<CollectionUserAccessTarget> targets;
        if (collectionIds.Count == 1)
        {
            var (collection, accessDetails) = await collectionRepository.GetByIdWithAccessAsync(collectionIds.Single());
            targets = collection is not null && collection.OrganizationId == orgId
                ? [new CollectionUserAccessTarget(collection, accessDetails)]
                : [];
        }
        else
        {
            var organizationCollections = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);
            targets = organizationCollections
                .Where(c => collectionIds.Contains(c.Item1.Id))
                .Select(c => new CollectionUserAccessTarget(c.Item1, c.Item2))
                .ToList();
        }

        if (targets.Count != collectionIds.Count)
        {
            throw new NotFoundException();
        }

        var resources = targets.Select(t => new CollectionUserAccessResource(t.Collection, t.AccessDetails)).ToList();

        // Authorize an empty delta too, so an empty request can't skip the check.
        if (addIds.Count == 0 && updateIds.Count == 0 && removeIds.Count == 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(user, resources, CollectionUserOperations.Update);
        }

        if (addIds.Count > 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(user, resources, CollectionUserOperations.Create);
        }

        if (updateIds.Count > 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(user, resources, CollectionUserOperations.Update);
        }

        if (removeIds.Count > 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(user, resources, CollectionUserOperations.Delete);
        }

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId);
        var callerOrganizationUser = currentContext.UserId.HasValue
            ? await organizationUserRepository.GetByOrganizationAsync(orgId, currentContext.UserId.Value)
            : null;

        var request = new ModifyCollectionUserAccessRequest(
            targets,
            add.Select(a => a.ToSelectionReadOnly()).ToList(),
            update.Select(u => u.ToSelectionReadOnly()).ToList(),
            removeIds,
            callerOrganizationUser?.Id,
            organizationAbility is { AllowAdminAccessToAllCollectionItems: true });

        var result = await modifyCollectionUserAccessCommand.ModifyAsync(request);
        return result.ToHttpResult();
    }
}
