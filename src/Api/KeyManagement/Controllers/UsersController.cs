using Bit.Api.KeyManagement.Models.Response;
using Bit.Api.KeyManagement.Queries.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserKeyResponseModel = Bit.Api.Models.Response.UserKeyResponseModel;

namespace Bit.Api.KeyManagement.Controllers;

[Route("users")]
[Authorize("Application")]
public class UsersController(
    IUserRepository _userRepository,
    IUserAccountKeysQuery _userAccountKeysQuery) : Controller
{
    [HttpGet("{id}/public-key")]
    public async Task<UserKeyResponseModel> GetPublicKeyAsync(string id)
    {
        var guidId = new Guid(id);
        var key = await _userRepository.GetPublicKeyAsync(guidId) ?? throw new NotFoundException();
        return new UserKeyResponseModel(guidId, key);
    }

    [HttpGet("{id}/keys")]
    public async Task<PublicKeysResponseModel> GetAccountKeysAsync(string id)
    {
        var guidId = new Guid(id);
        var user = await _userRepository.GetByIdAsync(guidId) ?? throw new NotFoundException();
        var accountKeys = await _userAccountKeysQuery.Run(user) ?? throw new NotFoundException("User account keys not found.");
        return new PublicKeysResponseModel(accountKeys);
    }
}
