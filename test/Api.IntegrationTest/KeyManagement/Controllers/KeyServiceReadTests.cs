using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.KeyManagement.Controllers;

public class KeyServiceReadTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    // An RSA public key whose SDK key id is 04fbcfa50c5805171304cc5b4794c25d.
    private const string PublicKey =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArvcXfr5pCD6KhzXo7BWc" +
        "5Hdcbgp9U6hk0+wDYQBJ2yP8mlbd3GiN9JMFAtliE6BaTYLuxI9Mdk7XmDoKy63X" +
        "AuI8tUon5imL/792Wca3f3qrbZh9pOfPKWp7HkcByty1ZO8QPlEYUP24y4DzOfVd" +
        "LkdZfs9X5qKHiTxc+VklzTm3PSap4eORTQ/lP1GB10y0qJk5+44GRcSQSr3ku6ui" +
        "2re8AJ2GQhdnZz5oWaCb/kij5bQPBwBrIEBlgRdaeasVdR6wFJPJAQZxtqWo9MPK" +
        "eVDOkaQ3Qrryh+49S4rln3592/WeHYM5hO47DJr86ELcqcyCmksYas7xTqHfVfHS" +
        "XQIDAQAB";
    private const string PublicKeyId = "04fbcfa50c5805171304cc5b4794c25d";
    private const string RootKeyId = "0123456789abcdef0123456789abcdef";

    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private string _email = null!;
    private Guid _userId;

    public KeyServiceReadTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_email);

        // A never-rotated V1 account with a real public key and a reported root key id.
        var user = (await _userRepository.GetByEmailAsync(_email))!;
        user.PublicKey = PublicKey;
        user.UserKeyId = RootKeyId;
        user.LastKeyRotationDate = null;
        await _userRepository.ReplaceAsync(user);
        _userId = user.Id;

        await _loginHelper.LoginAsync(_email);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublicKeys_DerivesKeyId()
    {
        var data = await GetDataAsync($"/v1/keys/public?filter[user]={_userId}");

        var key = Assert.Single(data.EnumerateArray());
        Assert.Equal("public-key", key.GetProperty("type").GetString());
        Assert.Equal(new Guid(PublicKeyId), key.GetProperty("id").GetGuid());

        var attributes = key.GetProperty("attributes");
        Assert.Equal("user", attributes.GetProperty("owner").GetProperty("type").GetString());
        Assert.Equal(_userId, attributes.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal("user-sharing-root", attributes.GetProperty("keyPurpose").GetString());
        Assert.Equal(PublicKeyId, attributes.GetProperty("keyId").GetString());
    }

    [Fact]
    public async Task PublicKeys_NoOwner_Unprocessable()
    {
        var response = await _client.GetAsync("/v1/keys/public");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task OwnKeys_ReturnsKeyAndMarker()
    {
        var data = await GetDataAsync("/v1/keys/own");

        var keys = data.EnumerateArray().ToList();
        Assert.Equal(["private-key", "symmetric-key"], keys.Select(key => key.GetProperty("type").GetString()));
        Assert.All(keys, key =>
            Assert.Equal(RootKeyId, key.GetProperty("attributes").GetProperty("sealedByKeyId").GetString()));
        Assert.Equal("00000000000000000000000000000000",
            keys[1].GetProperty("attributes").GetProperty("keyId").GetString());
    }

    [Fact]
    public async Task UnlockMethods_ReturnsMasterPassword()
    {
        var data = await GetDataAsync("/v1/unlock-methods");

        var method = Assert.Single(data.EnumerateArray());
        var attributes = method.GetProperty("attributes");
        Assert.Equal("master-password", attributes.GetProperty("kind").GetString());
        Assert.Equal(RootKeyId, attributes.GetProperty("containedKeyId").GetString());
        Assert.False(attributes.TryGetProperty("device", out _));

        using var unlockData = JsonDocument.Parse(attributes.GetProperty("data").GetString()!);
        Assert.Equal("raw", unlockData.RootElement.GetProperty("type").GetString());
        Assert.Equal(_email.ToLowerInvariant(), unlockData.RootElement.GetProperty("salt").GetString());

        var one = await GetDataAsync($"/v1/unlock-methods/{method.GetProperty("id").GetGuid()}");
        Assert.Equal("master-password", one.GetProperty("attributes").GetProperty("kind").GetString());
    }

    private async Task<JsonElement> GetDataAsync(string path)
    {
        var response = await _client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
