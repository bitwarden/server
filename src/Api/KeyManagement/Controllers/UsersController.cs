using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserKeyResponseModel = Bit.Api.Models.Response.UserKeyResponseModel;


namespace Bit.Api.KeyManagement.Controllers;

[Route("users")]
[Authorize("Application")]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IUserAccountKeysQuery _userAccountKeysQuery;

    public UsersController(IUserRepository userRepository, IUserAccountKeysQuery userAccountKeysQuery)
    {
        _userRepository = userRepository;
        _userAccountKeysQuery = userAccountKeysQuery;
    }

    [HttpGet("{id}/public-key")]
    public async Task<UserKeyResponseModel> GetPublicKeyAsync([FromRoute] Guid id)
    {
        var key = await _userRepository.GetPublicKeyAsync(id) ?? throw new NotFoundException();
        return new UserKeyResponseModel(id, key);
    }

    [HttpGet("{id}/keys")]
    public async Task<PublicKeysResponseModel> GetAccountKeysAsync([FromRoute] Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id) ?? throw new NotFoundException();
        var accountKeys = await _userAccountKeysQuery.Run(user) ?? throw new NotFoundException("User account keys not found.");
        return new PublicKeysResponseModel(accountKeys);
    }
}
