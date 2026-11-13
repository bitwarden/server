using Bit.Api.KeyManagement.Models.Responses;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

/// <summary>
/// The Key Service's unlock method reads, served from the existing key columns.
/// </summary>
[Route("v1/unlock-methods")]
[Authorize("Application")]
public class UnlockMethodsController(
    IUserService userService,
    IUnlockMethodsQuery unlockMethodsQuery) : Controller
{
    /// <summary>
    /// Lists the current user's unlock methods, only of <c>filter[kind]</c> when given.
    /// </summary>
    [HttpGet("")]
    public async Task<KeyServiceDocument<IEnumerable<KeyServiceResource<UnlockMethodAttributes>>>> GetAllAsync(
        [FromQuery(Name = "filter[kind]")] string? kind)
    {
        var user = await userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        var methods = await unlockMethodsQuery.Run(user, KeyServiceFilters.Values(kind));

        return new(methods.Select(UnlockMethodAttributes.Resource));
    }

    /// <summary>
    /// Gets one of the current user's unlock methods.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<KeyServiceDocument<KeyServiceResource<UnlockMethodAttributes>>> GetAsync(Guid id)
    {
        var user = await userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        var methods = await unlockMethodsQuery.Run(user, null);
        var method = methods.FirstOrDefault(method => method.Id == id) ?? throw new NotFoundException();

        return new(UnlockMethodAttributes.Resource(method));
    }
}
