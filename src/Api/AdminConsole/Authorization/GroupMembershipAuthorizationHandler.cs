using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Authorizes group membership changes made via GroupsController.
/// </summary>
public class GroupMembershipAuthorizationHandler
    : AuthorizationHandler<GroupOperationRequirement, GroupMembershipUpdateResource>
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IFeatureService _featureService;

    public GroupMembershipAuthorizationHandler(
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
        GroupMembershipUpdateResource resource)
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

        var currentIds = resource.CurrentMemberOrganizationUserIds.ToHashSet();
        var isAddingSelf = resource.PostedMemberOrganizationUserIds.Contains(actingOrganizationUser.Id)
                           && !currentIds.Contains(actingOrganizationUser.Id);

        if (isAddingSelf && !GroupMembershipAuthorizationRules.CanAddSelfToGroups(allowAdminAccess))
        {
            throw new BadRequestException("You cannot add yourself to groups.");
        }

        context.Succeed(requirement);
    }
}
