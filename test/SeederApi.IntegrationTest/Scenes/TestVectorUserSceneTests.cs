using System.Net;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Drives <see cref="TestVectorUserScene"/> through POST /seed with copies of the SDK's committed user vectors
/// (<c>sdk-internal/test-vectors/users</c>), and checks the server holds what the vector records, byte for byte.
/// In a play, so emails are mangled and the play's cleanup removes what was seeded.
/// </summary>
public class TestVectorUserSceneTests : IAsyncLifetime
{
    private readonly InPlaySeederApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _playId = Guid.NewGuid().ToString();

    public TestVectorUserSceneTests()
    {
        _factory = new InPlaySeederApiApplicationFactory();
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync($"/seed/{_playId}");
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static JsonElement Vector(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Scenes", "TestVectors", $"{name}.json")))
            .RootElement;

    private static JsonElement MasterPasswordUnlock(JsonElement vector) =>
        vector.GetProperty("unlockMethods").EnumerateArray()
            .First(method => method.TryGetProperty("masterPasswordUnlock", out _))
            .GetProperty("masterPasswordUnlock").GetProperty("master_password_unlock");

    private async Task<(User User, JsonElement Result)> SeedAsync(JsonElement vector)
    {
        var result = await _client.PostSceneAsync(_playId, nameof(TestVectorUserScene), vector);
        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdAsync(result.GetProperty("userId").GetGuid());
        Assert.NotNull(user);
        return (user!, result);
    }

    [Fact]
    public async Task SeedEndpoint_V2Vector_StoresTheAccountAndVaultAsRecorded()
    {
        var vector = Vector("v2-pbkdf2-blob");
        var unlock = MasterPasswordUnlock(vector);
        var raw = vector.GetProperty("rawCryptographicState");
        var v2 = vector.GetProperty("account").GetProperty("accountCryptographicState").GetProperty("V2");

        var (user, result) = await SeedAsync(vector);

        // 1. The account's keys are the vector's, under a fresh, mangled email and the vector's salt
        Assert.NotEqual(vector.GetProperty("account").GetProperty("email").GetString(), user.Email);
        Assert.Equal(unlock.GetProperty("salt").GetString(), user.GetMasterPasswordSalt());
        Assert.Equal(unlock.GetProperty("masterKeyWrappedUserKey").GetString(), user.Key);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(600_000, user.KdfIterations);
        Assert.Equal(raw.GetProperty("userKeyId").GetString(), user.UserKeyId);
        Assert.Equal(raw.GetProperty("publicKey").GetString(), user.PublicKey);
        Assert.Equal(v2.GetProperty("private_key").GetString(), user.PrivateKey);
        Assert.Equal(v2.GetProperty("signed_public_key").GetString(), user.SignedPublicKey);
        Assert.Equal(v2.GetProperty("security_state").GetString(), user.SecurityState);

        // 2. The master password authentication hash the vector records verifies
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(
            user, user.MasterPassword!, vector.GetProperty("masterPasswordAuthenticationHash").GetString()!));

        // 3. The signature key pair is the vector's, its algorithm read from the verifying key
        var signature = await scope.ServiceProvider.GetRequiredService<IUserSignatureKeyPairRepository>()
            .GetByUserIdAsync(user.Id);
        Assert.NotNull(signature);
        Assert.Equal(SignatureAlgorithm.MlDsa44, signature!.SignatureAlgorithm);
        Assert.Equal(v2.GetProperty("signing_key").GetString(), signature.WrappedSigningKey);
        Assert.Equal(raw.GetProperty("verifyingKey").GetString(), signature.VerifyingKey);

        // 4. Every blob cipher's data is stored as recorded, under the id the result names
        var ciphers = await scope.ServiceProvider.GetRequiredService<ICipherRepository>().GetManyByUserIdAsync(user.Id);
        foreach (var item in vector.GetProperty("vault").GetProperty("ciphers").EnumerateArray())
        {
            var seededId = result.GetProperty("cipherIds").GetProperty(item.GetProperty("id").GetString()!).GetGuid();
            var cipher = ciphers.Single(c => c.Id == seededId);
            var encrypted = item.GetProperty("encrypted");
            Assert.Equal(encrypted.GetProperty("data").GetString(), cipher.Data);
            Assert.Equal(encrypted.GetProperty("key").GetString(), cipher.Key);
        }

        // 5. So is every folder's name
        var folders = await scope.ServiceProvider.GetRequiredService<IFolderRepository>().GetManyByUserIdAsync(user.Id);
        foreach (var item in vector.GetProperty("vault").GetProperty("folders").EnumerateArray())
        {
            var seededId = result.GetProperty("folderIds").GetProperty(item.GetProperty("id").GetString()!).GetGuid();
            Assert.Equal(item.GetProperty("encrypted").GetProperty("name").GetString(), folders.Single(f => f.Id == seededId).Name);
        }
    }

    [Theory]
    [InlineData("v1-pbkdf2-min-iterations", KdfType.PBKDF2_SHA256)]
    [InlineData("v1-argon2id-password", KdfType.Argon2id)]
    public async Task SeedEndpoint_V1Vector_StoresTheAccountAndFieldLevelCiphers(string name, KdfType kdf)
    {
        var vector = Vector(name);
        var unlock = MasterPasswordUnlock(vector);

        var (user, result) = await SeedAsync(vector);

        // 1. The account is V1: the vector's wrapped keys, no signature key pair
        Assert.Equal(unlock.GetProperty("masterKeyWrappedUserKey").GetString(), user.Key);
        Assert.Equal(kdf, user.Kdf);
        Assert.Equal(
            vector.GetProperty("account").GetProperty("accountCryptographicState").GetProperty("V1").GetProperty("private_key").GetString(),
            user.PrivateKey);
        Assert.Null(user.SignedPublicKey);

        // 2. Every field-level cipher keeps its encrypted name and login password
        using var scope = _factory.Services.CreateScope();
        var ciphers = await scope.ServiceProvider.GetRequiredService<ICipherRepository>().GetManyByUserIdAsync(user.Id);
        foreach (var item in vector.GetProperty("vault").GetProperty("ciphers").EnumerateArray())
        {
            var seededId = result.GetProperty("cipherIds").GetProperty(item.GetProperty("id").GetString()!).GetGuid();
            var data = JsonDocument.Parse(ciphers.Single(c => c.Id == seededId).Data).RootElement;
            var encrypted = item.GetProperty("encrypted");
            Assert.Equal(encrypted.GetProperty("name").GetString(), data.GetProperty("Name").GetString());
            Assert.Equal(
                encrypted.GetProperty("login").GetProperty("password").GetString(),
                data.GetProperty("Password").GetString());
        }
    }

    [Fact]
    public async Task SeedEndpoint_SameVectorTwice_SeedsTwoSeparateAccounts()
    {
        var vector = Vector("v2-pbkdf2-blob");

        var (first, _) = await SeedAsync(vector);
        var (second, _) = await SeedAsync(vector);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Email, second.Email);
    }

    [Fact]
    public async Task DeleteEndpoint_RemovesTheSeededAccountAndItsVault()
    {
        var (user, _) = await SeedAsync(Vector("v2-pbkdf2-blob"));

        var response = await _client.DeleteAsync($"/seed/{_playId}");

        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(user.Id));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ICipherRepository>().GetManyByUserIdAsync(user.Id));
    }

    [Fact]
    public async Task SeedEndpoint_VectorWithoutMasterPassword_IsRefused()
    {
        var vector = JsonSerializer.SerializeToElement(new
        {
            name = "no-master-password",
            account = new { email = "kc@example.com", accountCryptographicState = new { V1 = new { private_key = "2.x" } } },
            unlockMethods = new[] { new { keyConnector = new { } } },
            rawCryptographicState = new { publicKey = "public" },
        });

        var response = await _client.PostSceneRequestAsync(_playId, nameof(TestVectorUserScene), vector);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
