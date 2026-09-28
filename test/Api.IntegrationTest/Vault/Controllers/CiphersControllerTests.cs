using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class CiphersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string ValidEncString =
        "2.AAECAwQFBgcICQoLDA0ODw==|aGVsbG8=|AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;

    private string _ownerEmail = null!;

    public CiphersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _cipherRepository = _factory.GetService<ICipherRepository>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Cipher> CreateCorruptLoginCipherAsync()
    {
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        return await _cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            UserId = user.Id,
            // Not valid JSON, and not a blob-encrypted ("format_version") payload either, so it
            // exercises the same JsonException path as the Fido2 credential check.
            Data = "not valid json{",
        });
    }

    [Fact]
    public async Task Get_CipherHasCorruptData_ReturnsGracefullyWithoutCrashing()
    {
        var cipher = await CreateCorruptLoginCipherAsync();

        var response = await _client.GetAsync($"/ciphers/{cipher.Id}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(cipher.Data, root.GetProperty("data").GetString());
        // The typed field could not be parsed from the corrupt Data, so it is left null rather
        // than causing the request to fail.
        Assert.Equal(JsonValueKind.Null, root.GetProperty("login").ValueKind);
    }

    [Fact]
    public async Task Put_CipherHasCorruptData_OverwritesSuccessfully()
    {
        var cipher = await CreateCorruptLoginCipherAsync();

        var model = new CipherRequestModel
        {
            Type = CipherType.Login,
            Name = ValidEncString,
            Data = "{}",
        };

        var response = await _client.PutAsJsonAsync($"/ciphers/{cipher.Id}", model);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedCipher = await _cipherRepository.GetByIdAsync(cipher.Id);
        Assert.NotNull(updatedCipher);
        Assert.Equal("{}", updatedCipher.Data);
    }
}
