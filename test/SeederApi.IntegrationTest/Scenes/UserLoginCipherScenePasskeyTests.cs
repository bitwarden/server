using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class UserLoginCipherScenePasskeyTests
{
    private static readonly string _userKey = RustSdkService.GenerateUserKeys("passkey@example.com", "asdfasdfasdf").Key;

    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "passkey@example.com" };
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICipherRepository _cipherRepository = Substitute.For<ICipherRepository>();
    private readonly IFolderRepository _folderRepository = Substitute.For<IFolderRepository>();
    private readonly IManglerService _manglerService = Substitute.For<IManglerService>();

    public UserLoginCipherScenePasskeyTests()
    {
        _userRepository.GetByIdAsync(_user.Id).Returns(_user);
        _folderRepository.GetManyByUserIdAsync(_user.Id).Returns([]);
        _manglerService.GetMangleMap().Returns([]);
    }

    [Fact]
    public async Task SeedAsync_PasskeyWithKnownKeyMaterial_PersistsEachFieldUnswapped()
    {
        Cipher? saved = null;
        await _cipherRepository.CreateAsync(Arg.Do<Cipher>(c => saved = c));

        await new UserLoginCipherScene(_userRepository, _cipherRepository, _folderRepository, _manglerService)
            .SeedAsync(new UserLoginCipherScene.Request
            {
                UserId = _user.Id,
                UserKeyB64 = _userKey,
                Name = "Login",
                Passkeys =
                [
                    new()
                    {
                        RpId = "rp.example",
                        RpName = "RP Name",
                        UserName = "user-name",
                        CredentialId = "5096ee6c-448c-47a6-bfad-28dd9e14d4c6",
                        KeyValue = "key-value",
                        UserHandle = "user-handle",
                        UserDisplayName = "display-name"
                    }
                ]
            });

        var passkey = JsonSerializer.Deserialize<CipherLoginData>(saved!.Data)!.Fido2Credentials.Single();
        Assert.Equal("rp.example", Decrypt(passkey.RpId));
        Assert.Equal("RP Name", Decrypt(passkey.RpName));
        Assert.Equal("user-name", Decrypt(passkey.UserName));
        Assert.Equal("5096ee6c-448c-47a6-bfad-28dd9e14d4c6", Decrypt(passkey.CredentialId));
        Assert.Equal("key-value", Decrypt(passkey.KeyValue));
        Assert.Equal("user-handle", Decrypt(passkey.UserHandle));
        Assert.Equal("display-name", Decrypt(passkey.UserDisplayName));
    }

    private static string Decrypt(string encString) => RustSdkService.DecryptString(encString, _userKey);
}
