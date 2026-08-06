using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserLoginCipherScene(
    IUserRepository userRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService)
    : LoginCipherScene<UserLoginCipherScene.Request>(manglerService)
{
    public class Request : LoginCipherRequest
    {
        [Required]
        public required string UserKeyB64 { get; set; }
    }

    protected override async Task<CipherOwner> ResolveOwnerAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new Exception($"User with ID {request.UserId} not found.");
        }

        return new CipherOwner(request.UserKeyB64, null, request.UserId);
    }

    protected override Task PersistAsync(Cipher cipher, Request request)
        => cipherRepository.CreateAsync(cipher);
}
