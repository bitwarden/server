using Bit.Api.KeyManagement.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserKeyResponseModel = Bit.Api.Models.Response.UserKeyResponseModel;

namespace Bit.Api.Controllers;

[Route("users")]
[Authorize("Application")]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSignatureKeyPairRepository _signatureKeyPairRepository;

    public UsersController(
        IUserRepository userRepository,
        IUserSignatureKeyPairRepository signatureKeyPairRepository)
    {
        _userRepository = userRepository;
        _signatureKeyPairRepository = signatureKeyPairRepository;
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

    [HttpGet("{id}/keys")]
    public async Task<PublicKeysResponseModel> GetAccountKeys(string id)
    {
        var guidId = new Guid(id);
        var user = await _userRepository.GetByIdAsync(guidId);
        if (user == null)
        {
            throw new NotFoundException();
        }

        var signingKeys = await _signatureKeyPairRepository.GetByUserIdAsync(guidId);
        var verifyingKey = signingKeys?.VerifyingKey;

        return new PublicKeysResponseModel(verifyingKey, user.PublicKey, null);
    }
}
