using Asp.Versioning;
using Bit.Api.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[ApiVersion(1.0)]
[ApiVersion(2.0)]
[ApiVersion(3.0)]
[ApiController]
[Route("v{version:apiVersion}/users")]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;

    public UsersController(
        IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet("{id}/public-key")]
    public async Task<UserKeyResponseModel> Get(string id)
    {
        var guidId = new Guid(id);
        var key = await _userRepository.GetPublicKeyAsync(guidId);
        if (key == null)
        {
            throw new NotFoundException();
        }

        return new UserKeyResponseModel(guidId, key);
    }

    [HttpGet("{id}/test")]
    public async Task<String> GetTest(string id)
    {
        var guidId = new Guid(id);
        var key = await _userRepository.GetPublicKeyAsync(guidId);
        if (key == null)
        {
            throw new NotFoundException();
        }

        return key;
    }

    [HttpGet("{id}/public-key"), MapToApiVersion(2.0)]
    public async Task<UserKeyResponseModel> GetV2(string id)
    {
        var guidId = new Guid(id);
        var key = await _userRepository.GetPublicKeyAsync(guidId);
        if (key == null)
        {
            throw new NotFoundException();
        }

        return new UserKeyResponseModel(guidId, key);
    }
}
