using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Authorizes changes to which groups a user belongs to, made via OrganizationUsersController.
/// </summary>
public class OrganizationUserGroupAssignmentAuthorizationHandler
    : AuthorizationHandler<GroupOperationRequirement, OrganizationUserGroupAssignmentResource>
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IFeatureService _featureService;

    public OrganizationUserGroupAssignmentAuthorizationHandler(
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IFeatureService featureService)
    {
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupOperationRequirement requirement,
        OrganizationUserGroupAssignmentResource resource)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers))
        {
            return;
        }

        if (requirement != GroupOperations.UpdateMembership)
        {
            return;
        }

        var ability = await _applicationCacheService.GetOrganizationAbilityAsync(resource.OrganizationId);
        var allowAdminAccess = ability is { AllowAdminAccessToAllCollectionItems: true };

        var actingOrganizationUser = await _organizationUserRepository.GetByOrganizationAsync(
            resource.OrganizationId, resource.ActingUserId);

        if (actingOrganizationUser == null)
        {
            // Acting user is not an org member (e.g. provider); allow.
            context.Succeed(requirement);
            return;
        }

        if (actingOrganizationUser.Id != resource.TargetOrganizationUserId)
        {
            context.Succeed(requirement);
            return;
        }

        var currentIds = resource.CurrentGroupIds.ToHashSet();
        var isAddingToNewGroup = resource.PostedGroupIds.Any(id => !currentIds.Contains(id));

        if (isAddingToNewGroup && !GroupMembershipAuthorizationRules.CanAddSelfToGroups(allowAdminAccess))
        {
            throw new BadRequestException("You cannot add yourself to groups.");
        }

        context.Succeed(requirement);
    }
}
