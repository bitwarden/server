using System.Diagnostics;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Authorizes changes to group access across a set of collections.
/// </summary>
public class BulkCollectionGroupAuthorizationHandler
    : BulkAuthorizationHandler<CollectionGroupOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;

    public BulkCollectionGroupAuthorizationHandler(
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
        CollectionGroupOperationRequirement requirement, ICollection<Collection>? resources)
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
            not null when requirement == CollectionGroupOperations.Create =>
                resources.All(c => CollectionGroupAuthorizationRules.CanModifyGroupAccess(c, organization, authorizationContext)),
            not null when requirement == CollectionGroupOperations.Update =>
                resources.All(c => CollectionGroupAuthorizationRules.CanModifyGroupAccess(c, organization, authorizationContext)),
            not null when requirement == CollectionGroupOperations.Delete =>
                resources.All(c => CollectionGroupAuthorizationRules.CanModifyGroupAccess(c, organization, authorizationContext)),
            null => throw new UnreachableException(),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private Task<CollectionAccessAuthorizationContext> BuildContextAsync(
        Guid organizationId,
        CurrentContextOrganization? organization) =>
        CollectionAccessContextFactory.BuildAsync(
            organizationId, organization, _currentContext, _collectionRepository, _applicationCacheService);
}
