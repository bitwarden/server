using System.Diagnostics;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Authorizes changes to a specific user's access to collections.
/// </summary>
public class CollectionUserAuthorizationHandler
    : AuthorizationHandler<CollectionUserOperationRequirement, CollectionUserAccessResource>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;
    private readonly CollectionAccessContextService _collectionAccessContextService;

    public CollectionUserAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService,
        IFeatureService featureService,
        CollectionAccessContextService collectionAccessContextService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _applicationCacheService = applicationCacheService;
        _featureService = featureService;
        _collectionAccessContextService = collectionAccessContextService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, CollectionUserAccessResource resource)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers))
        {
            return;
        }

        var collections = resource.Collections;
        if (collections == null || !collections.Any())
        {
            return;
        }

        if (!_currentContext.UserId.HasValue)
        {
            return;
        }

        var targetOrganizationId = collections.First().OrganizationId;

        if (collections.Any(r => r.OrganizationId != targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        var organization = _currentContext.GetOrganization(targetOrganizationId);
        var authorizationContext = await _collectionAccessContextService.GetOrBuildAsync(
            targetOrganizationId, organization, _currentContext, _collectionRepository, _applicationCacheService);

        bool authorized;
        if (requirement == CollectionUserOperations.Create)
        {
            var callerOrganizationUser = organization != null
                ? await _organizationUserRepository.GetByOrganizationAsync(targetOrganizationId, _currentContext.UserId!.Value)
                : null;
            authorized = CanCreateUserAccess(
                resource, organization,
                authorizationContext with { CallerOrganizationUserId = callerOrganizationUser?.Id });
        }
        else if (requirement == CollectionUserOperations.Update || requirement == CollectionUserOperations.Delete)
        {
            authorized = collections.All(c =>
                CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, authorizationContext));
        }
        else if (requirement == null)
        {
            throw new UnreachableException();
        }
        else
        {
            authorized = false;
        }

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private static bool CanCreateUserAccess(
        CollectionUserAccessResource resource,
        CurrentContextOrganization? organization,
        CollectionAccessContext collectionAccessContext)
    {
        var editingSelf = collectionAccessContext.CallerOrganizationUserId.HasValue &&
                          collectionAccessContext.CallerOrganizationUserId.Value == resource.TargetOrganizationUserId;

        if (editingSelf && !CollectionUserAuthorizationRules.CanAddSelf(collectionAccessContext.AllowAdminAccessToAllCollectionItems))
        {
            throw new BadRequestException("You cannot add yourself to a collection.");
        }

        return resource.Collections.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, collectionAccessContext));
    }
}
