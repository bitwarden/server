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

    public CollectionUserAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
        _applicationCacheService = applicationCacheService;
        _featureService = featureService;
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
        var authorizationContext = await BuildContextAsync(targetOrganizationId, organization);

        var authorized = requirement switch
        {
            not null when requirement == CollectionUserOperations.Create =>
                CanCreateUserAccess(resource, organization, authorizationContext),
            not null when requirement == CollectionUserOperations.Update =>
                collections.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, authorizationContext)),
            not null when requirement == CollectionUserOperations.Delete =>
                collections.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, authorizationContext)),
            null => throw new UnreachableException(),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private static bool CanCreateUserAccess(
        CollectionUserAccessResource resource,
        CurrentContextOrganization? organization,
        CollectionAccessAuthorizationContext ctx)
    {
        var editingSelf = ctx.CallerOrganizationUserId.HasValue &&
                          ctx.CallerOrganizationUserId.Value == resource.TargetOrganizationUserId;

        if (editingSelf && !CollectionUserAuthorizationRules.CanAddSelf(ctx.AllowAdminAccessToAllCollectionItems))
        {
            throw new BadRequestException("You cannot add yourself to a collection.");
        }

        return resource.Collections.All(c => CollectionUserAuthorizationRules.CanModifyUserAccess(c, organization, ctx));
    }

    private async Task<CollectionAccessAuthorizationContext> BuildContextAsync(
        Guid organizationId,
        CurrentContextOrganization? organization)
    {
        var ctx = await CollectionAccessContextFactory.BuildAsync(
            organizationId, organization, _currentContext, _collectionRepository, _applicationCacheService);

        var callerOrgUser = organization != null
            ? await _organizationUserRepository.GetByOrganizationAsync(organizationId, _currentContext.UserId!.Value)
            : null;

        return ctx with { CallerOrganizationUserId = callerOrgUser?.Id };
    }
}
