#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class OrganizationUserUserMiniDetailsQuery : IOrganizationUserUserMiniDetailsQuery
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public OrganizationUserUserMiniDetailsQuery(
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IOrganizationUserRepository organizationUserRepository)
    {
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _organizationUserRepository = organizationUserRepository;
    }
    public async Task<IEnumerable<OrganizationUserUserMiniDetails>> Get(Guid orgId)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
            new OrganizationScopeResource(orgId), OrganizationUserUserMiniDetailsOperations.ReadAll);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var organizationUserUserDetails = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(orgId);
        return organizationUserUserDetails.Select(ou => new OrganizationUserUserMiniDetails(ou));
    }
}
