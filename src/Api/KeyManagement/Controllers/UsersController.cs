using Bit.Api.KeyManagement.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

[Route("users")]
[Authorize("Application")]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSigningKeysRepository _signingKeysRepository;

    public UsersController(
        IUserRepository userRepository,
        IUserSigningKeysRepository signingKeysRepository
    )
    {
        _userRepository = userRepository;
        _signingKeysRepository = signingKeysRepository;
    }

    [HttpGet("{id}/public-key")]
    public async Task<UserKeyResponseModel> GetPublicKey(string id)
    {
        var guidId = new Guid(id);
        var key = await _userRepository.GetPublicKeyAsync(guidId);
        if (key == null)
        {
            throw new NotFoundException();
        }

        return new UserKeyResponseModel(guidId, key);
    }

    [HttpGet("{id}/keys")]
    public async Task<PublicUserKeysResponseModel> GetAccountKeys(string id)
    {
        var guidId = new Guid(id);
        var user = await _userRepository.GetByIdAsync(guidId);
        if (user == null)
        {
            throw new NotFoundException();
        }

        var signingKeys = await _signingKeysRepository.GetByUserIdAsync(guidId);
        var verifyingKey = signingKeys?.VerifyingKey;

        return new PublicUserKeysResponseModel(verifyingKey, user.PublicKey, null);
    }
}
