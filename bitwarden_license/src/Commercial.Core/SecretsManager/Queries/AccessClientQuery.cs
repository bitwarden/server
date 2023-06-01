using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.Services;

namespace Bit.Commercial.Core.SecretsManager.Queries;

public class AccessClientQuery : IAccessClientQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;

    public AccessClientQuery(ICurrentContext currentContext, IUserService userService)
    {
        _currentContext = currentContext;
        _userService = userService;
    }

    public async Task<(AccessClientType AccessClientType, Guid UserId)> GetAccessClientAsync(
        ClaimsPrincipal claimsPrincipal, Guid organizationId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var userId = _userService.GetProperUserId(claimsPrincipal).Value;
        return (accessClient, userId);
    }
}
