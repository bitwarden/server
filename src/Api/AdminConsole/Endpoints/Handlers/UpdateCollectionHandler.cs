using System.Security.Claims;
using System.Transactions;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Utilities;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Endpoints.Handlers;

/// <summary>
/// Handles the unified <c>PUT organizations/{orgId}/collections/{id}</c> endpoint: updates a collection's
/// metadata alongside add/update/remove deltas for its user and group access. Each concern is authorized
/// against its own requirement, and the three writes commit as a single atomic transaction so a partial
/// failure never leaves a collection with metadata applied but access changes rejected (or vice versa).
/// </summary>
public class UpdateCollectionHandler(
    ICollectionRepository collectionRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IAuthorizationService authorizationService,
    ICurrentContext currentContext,
    IUpdateCollectionCommand updateCollectionCommand,
    IModifyCollectionUserAccessCommand modifyCollectionUserAccessCommand,
    IModifyCollectionGroupAccessCommand modifyCollectionGroupAccessCommand)
{
    public async Task<IResult> HandleAsync(
        Guid orgId,
        Guid id,
        UpdateCollectionWithDeltaRequestModel model,
        ClaimsPrincipal user)
    {
        // Resolve the target collection once with its full access details; the same tuple feeds both
        // the update itself and the two access-resource authorization checks that follow.
        var (collection, accessDetails) = await collectionRepository.GetByIdWithAccessAsync(id);
        if (collection is null || collection.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await authorizationService.AuthorizeOrThrowAsync(user, collection, BulkCollectionOperations.Update);

        var userResource = new CollectionUserAccessResource(collection, accessDetails);
        await authorizationService.AuthorizeOrThrowAsync(user, userResource, CollectionUserOperations.Update);

        var groupResource = new CollectionGroupAccessResource(collection, accessDetails);
        await authorizationService.AuthorizeOrThrowAsync(user, groupResource, CollectionGroupOperations.Update);

        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId);
        var allowAdminAccessToAllCollectionItems =
            organizationAbility is { AllowAdminAccessToAllCollectionItems: true };

        var callerOrganizationUser = currentContext.UserId.HasValue
            ? await organizationUserRepository.GetByOrganizationAsync(orgId, currentContext.UserId.Value)
            : null;

        var userTargets = new[] { new CollectionUserAccessTarget(collection, accessDetails) };
        var groupTargets = new[] { new CollectionGroupAccessTarget(collection, accessDetails) };

        var userRequest = new ModifyCollectionUserAccessRequest(
            userTargets,
            model.Users.Add.Select(u => u.ToSelectionReadOnly()).ToList(),
            model.Users.Update.Select(u => u.ToSelectionReadOnly()).ToList(),
            model.Users.Remove.ToList(),
            callerOrganizationUser?.Id,
            allowAdminAccessToAllCollectionItems);

        var groupRequest = new ModifyCollectionGroupAccessRequest(
            groupTargets,
            model.Groups.Add.Select(g => g.ToSelectionReadOnly()).ToList(),
            model.Groups.Update.Select(g => g.ToSelectionReadOnly()).ToList(),
            model.Groups.Remove.ToList(),
            callerOrganizationUser?.Id,
            allowAdminAccessToAllCollectionItems);

        // Bail before opening a transaction if either access delta already knows it's invalid — the
        // access commands emit typed CommandResult errors instead of throwing, so we surface those
        // as the HTTP response and skip persisting the metadata update.
        CommandResult? failedResult = null;

        using (var scope = new TransactionScope(
                   TransactionScopeOption.Required,
                   new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                   TransactionScopeAsyncFlowOption.Enabled))
        {
            // Apply Name/ExternalId first so subsequent access changes see the current revision. The
            // update command itself throws BadRequestException on invalid input, which the endpoint
            // filter translates into the shared ErrorResponseModel contract.
            ApplyMetadataChanges(collection, model);
            await updateCollectionCommand.UpdateAsync(collection);

            var userResult = await modifyCollectionUserAccessCommand.ModifyAsync(userRequest);
            if (userResult.IsError)
            {
                failedResult = userResult;
            }
            else
            {
                var groupResult = await modifyCollectionGroupAccessCommand.ModifyAsync(groupRequest);
                if (groupResult.IsError)
                {
                    failedResult = groupResult;
                }
            }

            if (failedResult is null)
            {
                scope.Complete();
            }
        }

        return failedResult is not null
            ? failedResult.ToHttpResult()
            : TypedResults.NoContent();
    }

    private static void ApplyMetadataChanges(Bit.Core.Entities.Collection collection, UpdateCollectionWithDeltaRequestModel model)
    {
        // Mirror the existing MVC UpdateCollectionRequestModel behaviour: leave a default user collection's
        // name untouched, and always accept an ExternalId (including clearing it).
        if (string.IsNullOrEmpty(collection.DefaultUserCollectionEmail) && !string.IsNullOrWhiteSpace(model.Name))
        {
            collection.Name = model.Name;
        }

        collection.ExternalId = model.ExternalId;
    }
}
