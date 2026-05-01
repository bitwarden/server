using System.Diagnostics;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Authorizes changes to user access across a set of collections.
/// </summary>
public class BulkCollectionUserAuthorizationHandler
    : BulkAuthorizationHandler<CollectionUserOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;

    public BulkCollectionUserAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IApplicationCacheService applicationCacheService,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _applicationCacheService = applicationCacheService;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, ICollection<Collection>? resources)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers))
        {
            return;
        }

        if (resources == null || !resources.Any())
        {
            return;
        }

        if (!_currentContext.UserId.HasValue)
        {
            return;
        }

        var targetOrganizationId = resources.First().OrganizationId;

        if (resources.Any(r => r.OrganizationId != targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        var organization = _currentContext.GetOrganization(targetOrganizationId);
        var authorizationContext = await BuildContextAsync(targetOrganizationId, organization);

        var authorized = requirement switch
        {
            not null when requirement == CollectionUserOperations.Create =>
                resources.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, authorizationContext)),
            not null when requirement == CollectionUserOperations.Update =>
                resources.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, authorizationContext)),
            not null when requirement == CollectionUserOperations.Delete =>
                resources.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, authorizationContext)),
            null => throw new UnreachableException(),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<CollectionAccessAuthorizationContext> BuildContextAsync(
        Guid organizationId,
        CurrentContextOrganization? organization)
    {
        var ability = organization != null
            ? await _applicationCacheService.GetOrganizationAbilityAsync(organization.Id)
            : null;

        var allowAdminAccess = ability is { AllowAdminAccessToAllCollectionItems: true };
        var isProviderUser = await _currentContext.ProviderUserForOrgAsync(organizationId);

        var allUserCollections = await _collectionRepository
            .GetManyByUserIdAsync(_currentContext.UserId!.Value);

        var managedCollectionIds = allUserCollections
            .Where(c => c.Manage)
            .Select(c => c.Id)
            .ToHashSet();

        HashSet<Guid> orphanedCollectionIds;
        if (organization is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin }
            or { Permissions.EditAnyCollection: true })
        {
            var organizationCollections = await _collectionRepository
                .GetManyByOrganizationIdWithAccessAsync(organizationId);
            orphanedCollectionIds = organizationCollections
                .Where(c => !c.Item2.Users.Any(u => u.Manage) && !c.Item2.Groups.Any(g => g.Manage))
                .Select(c => c.Item1.Id)
                .ToHashSet();
        }
        else
        {
            orphanedCollectionIds = [];
        }

        return new CollectionAccessAuthorizationContext(
            AllowAdminAccessToAllCollectionItems: allowAdminAccess,
            CallerIsProviderUser: isProviderUser,
            CallerManagedCollectionIds: managedCollectionIds,
            OrphanedCollectionIds: orphanedCollectionIds);
    }
}
