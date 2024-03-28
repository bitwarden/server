#nullable enable

using System.Security.Claims;
using Bit.Api.Models.Request;
using Bit.Api.Utilities;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class CollectionAuthorizationHelpers : ICollectionAuthorizationHelpers
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICurrentContext _currentContext;

    public CollectionAuthorizationHelpers(
        IAuthorizationService authorizationService,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICurrentContext currentContext)
    {
        _authorizationService = authorizationService;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _currentContext = currentContext;
    }

    public async Task<List<CollectionAccessSelection>> GetCollectionAccessToUpdateAsync(
        ClaimsPrincipal user,
        OrganizationAbility organizationAbility,
        IEnumerable<SelectionReadOnlyRequestModel>? updatedCollectionAccess)
    {
        var collections = updatedCollectionAccess?.Select(c => c.ToSelectionReadOnly()).ToList() ?? [];

        // If the current user can edit any collection, we can safely replace all the target orgUser's collection access
        var canEditAnyCollection =
            (await _authorizationService.AuthorizeAsync(user, CollectionOperations.EditAll(organizationAbility.Id))).Succeeded;
        if (!organizationAbility.FlexibleCollections || canEditAnyCollection)
        {
            return collections;
        }

        // If the current user cannot edit any collection, we need to add existing readonly collection associations

        // get the target orgUser's current collection associations
        var (_, targetUserCurrentCollections) =
            await _organizationUserRepository.GetByIdWithCollectionsAsync(organizationAbility.Id);

        // get the saving user's current collection permissions
        var savingUserCollections =
            await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value, false);
        var editableCollectionIds = savingUserCollections
            .Where(c => c.Manage)
            .Select(c => c.Id)
            .ToHashSet();

        // identify the collections we can't edit
        var readonlyAssociations =
            targetUserCurrentCollections
                .Where(cas => !editableCollectionIds.Contains(cas.Id))
                .ToList();

        // Make sure we're not trying to create or modify access to a collection we can't manage
        var readonlyAssociationIds = readonlyAssociations
            .Select(cas => cas.Id)
            .ToHashSet();
        if (collections.Any(c => readonlyAssociationIds.Contains(c.Id)))
        {
            throw new BadRequestException("You must have Can Manage permissions to edit a collection's membership");
        }

        return collections.Concat(readonlyAssociations).ToList();
    }
}
