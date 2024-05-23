using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Vault.AuthorizationHandlers.Groups;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Api.AdminConsole.Services;

public class GroupsControllerService : IGroupsControllerService
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IGroupRepository _groupRepository;

    public GroupsControllerService(
        ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        IApplicationCacheService applicationCacheService,
        IGroupRepository groupRepository)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _applicationCacheService = applicationCacheService;
        _groupRepository = groupRepository;
    }

    public async Task<IEnumerable<GroupDetailsResponseModel>> GetGroups(ClaimsPrincipal user, Guid orgId)
    {
        if (await FlexibleCollectionsIsEnabledAsync(orgId))
        {
            // New flexible collections logic
            return await Get_vNext(user, orgId);
        }

        // Old pre-flexible collections logic follows
        var canAccess = await _currentContext.ManageGroups(orgId) ||
                        await _currentContext.ViewAssignedCollections(orgId) ||
                        await _currentContext.ViewAllCollections(orgId) ||
                        await _currentContext.ManageUsers(orgId);

        if (!canAccess)
        {
            throw new NotFoundException();
        }

        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
        return responses;
    }

    private async Task<IEnumerable<GroupDetailsResponseModel>> Get_vNext(ClaimsPrincipal user, Guid orgId)
    {
        var authorized =
            (await _authorizationService.AuthorizeAsync(user, GroupOperations.ReadAll(orgId))).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var groups = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        return groups.Select(g => new GroupDetailsResponseModel(g.Item1, g.Item2));
    }

    private async Task<bool> FlexibleCollectionsIsEnabledAsync(Guid organizationId)
    {
        var organizationAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return organizationAbility?.FlexibleCollections ?? false;
    }
}
