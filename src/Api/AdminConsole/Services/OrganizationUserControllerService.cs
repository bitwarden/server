using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Utilities;
using Bit.Api.Vault.AuthorizationHandlers.OrganizationUsers;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Api.AdminConsole.Services;

public class OrganizationUserControllerService : IOrganizationUserControllerService
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IAuthorizationService _authorizationService;

    public OrganizationUserControllerService(
        IOrganizationUserRepository organizationUserRepository,
        IUserService userService,
        ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        IApplicationCacheService applicationCacheService)
    {
        _organizationUserRepository = organizationUserRepository;
        _userService = userService;
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _applicationCacheService = applicationCacheService;
    }

    public async Task<IEnumerable<OrganizationUserUserDetailsResponseModel>> GetOrganizationUserUserDetails(
        ClaimsPrincipal user,
        Guid orgId,
        bool includeGroups = false,
        bool includeCollections = false)
    {
        if (await FlexibleCollectionsIsEnabledAsync(orgId))
        {
            return await Get_vNext(user, orgId, includeGroups, includeCollections);
        }

        var authorized = await _currentContext.ViewAllCollections(orgId) ||
            await _currentContext.ViewAssignedCollections(orgId) ||
            await _currentContext.ManageGroups(orgId) ||
            await _currentContext.ManageUsers(orgId);
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var organizationUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(orgId, includeGroups, includeCollections);
        var responseTasks = organizationUsers.Select(async o => new OrganizationUserUserDetailsResponseModel(o,
            await _userService.TwoFactorIsEnabledAsync(o)));
        var responses = await Task.WhenAll(responseTasks);
        return responses.ToList();
    }

    public async Task<bool> FlexibleCollectionsIsEnabledAsync(Guid organizationId)
    {
        var organizationAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return organizationAbility?.FlexibleCollections ?? false;
    }

    public async Task<IEnumerable<OrganizationUserUserDetailsResponseModel>> Get_vNext(ClaimsPrincipal user, Guid orgId,
        bool includeGroups = false, bool includeCollections = false)
    {
        if (user == null)
        {
            throw new NotFoundException();
        }

        var authorized = (await _authorizationService.AuthorizeAsync(
            user, OrganizationUserOperations.ReadAll(orgId))).Succeeded;
        if (!authorized)
        {
            throw new NotFoundException();
        }

        var organizationUsers = await _organizationUserRepository
            .GetManyDetailsByOrganizationAsync(orgId, includeGroups, includeCollections);
        var responseTasks = organizationUsers
            .Select(async o =>
            {
                var orgUser = new OrganizationUserUserDetailsResponseModel(o,
                    await _userService.TwoFactorIsEnabledAsync(o));

                // Downgrade Custom users with no other permissions than 'Edit/Delete Assigned Collections' to User
                orgUser.Type = GetFlexibleCollectionsUserType(orgUser.Type, orgUser.Permissions);

                // Set 'Edit/Delete Assigned Collections' custom permissions to false
                if (orgUser.Permissions is not null)
                {
                    orgUser.Permissions.EditAssignedCollections = false;
                    orgUser.Permissions.DeleteAssignedCollections = false;
                }

                return orgUser;
            });
        var responses = await Task.WhenAll(responseTasks);

        return responses;
    }

    public OrganizationUserType GetFlexibleCollectionsUserType(OrganizationUserType type, Permissions permissions)
    {
        // Downgrade Custom users with no other permissions than 'Edit/Delete Assigned Collections' to User
        if (type == OrganizationUserType.Custom && permissions is not null)
        {
            if ((permissions.EditAssignedCollections || permissions.DeleteAssignedCollections) &&
                permissions is
                {
                    AccessEventLogs: false,
                    AccessImportExport: false,
                    AccessReports: false,
                    CreateNewCollections: false,
                    EditAnyCollection: false,
                    DeleteAnyCollection: false,
                    ManageGroups: false,
                    ManagePolicies: false,
                    ManageSso: false,
                    ManageUsers: false,
                    ManageResetPassword: false,
                    ManageScim: false
                })
            {
                return OrganizationUserType.User;
            }
        }

        return type;
    }
}
