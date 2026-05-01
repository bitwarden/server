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
/// Authorizes changes to user access across a set of collections.
/// </summary>
public class BulkCollectionUserAuthorizationHandler
    : BulkAuthorizationHandler<CollectionUserOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;
    private readonly CollectionAccessContextService _collectionAccessContextService;

    public BulkCollectionUserAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IApplicationCacheService applicationCacheService,
        IFeatureService featureService,
        CollectionAccessContextService collectionAccessContextService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _applicationCacheService = applicationCacheService;
        _featureService = featureService;
        _collectionAccessContextService = collectionAccessContextService;
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
        var collectionAccessContext = await BuildCollectionAccessContextAsync(targetOrganizationId, organization);

        var authorized = requirement switch
        {
            not null when requirement == CollectionUserOperations.Create =>
                resources.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, collectionAccessContext)),
            not null when requirement == CollectionUserOperations.Update =>
                resources.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, collectionAccessContext)),
            not null when requirement == CollectionUserOperations.Delete =>
                resources.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, collectionAccessContext)),
            null => throw new UnreachableException(),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private Task<CollectionAccessContext> BuildCollectionAccessContextAsync(
        Guid organizationId,
        CurrentContextOrganization? organization) =>
        _collectionAccessContextService.GetOrBuildAsync(
            organizationId, organization, _currentContext, _collectionRepository, _applicationCacheService);
}
