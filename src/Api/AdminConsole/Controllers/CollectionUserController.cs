using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

/// <summary>
/// Handles add/update/remove changes to a collection's user access. The bulk route applies the same
/// change to every listed collection.
/// </summary>
[Route("organizations/{orgId:guid}/collections")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.PM12473CollectionUserAccessEndpoint)]
public class CollectionUserController(
    ICollectionRepository collectionRepository,
    IAuthorizationService authorizationService,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ICurrentContext currentContext,
    IModifyCollectionUserAccessCommand modifyCollectionUserAccessCommand)
    : BaseAdminConsoleController
{
    [NoopAuthorize]
    [HttpPatch("{id:guid}/users")]
    public Task<IResult> PatchCollectionUserAccessAsync(Guid orgId, Guid id, [FromBody] CollectionUserAccessDeltaRequestModel model)
        => ModifyUserAccessAsync(orgId, [id], model.Add, model.Update, model.Remove);

    [NoopAuthorize]
    [HttpPatch("users")]
    public Task<IResult> PatchBulkCollectionUserAccessAsync(Guid orgId, [FromBody] BulkCollectionUserAccessDeltaRequestModel model)
        => ModifyUserAccessAsync(orgId, model.CollectionIds.ToList(), model.Add, model.Update, model.Remove);

    private async Task<IResult> ModifyUserAccessAsync(
        Guid orgId, IReadOnlyCollection<Guid> collectionIds,
        IEnumerable<SelectionReadOnlyRequestModel> add, IEnumerable<SelectionReadOnlyRequestModel> update,
        IEnumerable<Guid> remove)
    {
        var addIds = add.Select(a => a.Id).ToHashSet();
        var updateIds = update.Select(u => u.Id).ToHashSet();
        var removeIds = remove.ToHashSet();

        var organizationCollections = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);
        var targets = organizationCollections
            .Where(c => collectionIds.Contains(c.Item1.Id))
            .Select(c => new CollectionUserAccessTarget(c.Item1, c.Item2))
            .ToList();
        if (targets.Count != collectionIds.Count)
        {
            throw new NotFoundException();
        }

        var resources = targets.Select(t => new CollectionUserAccessResource(t.Collection, t.AccessDetails)).ToList();

        // Check authorization even for an empty delta, so an empty request can't skip it.
        if (addIds.Count == 0 && updateIds.Count == 0 && removeIds.Count == 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(User, resources, CollectionUserOperations.Update);
        }

        if (addIds.Count > 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(User, resources, CollectionUserOperations.Create);
        }

        if (updateIds.Count > 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(User, resources, CollectionUserOperations.Update);
        }

        if (removeIds.Count > 0)
        {
            await authorizationService.AuthorizeOrThrowAsync(User, resources, CollectionUserOperations.Delete);
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
        return Handle(result);
    }
}
