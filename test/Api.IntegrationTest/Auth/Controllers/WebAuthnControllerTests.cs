using System.Text.Json;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.Auth.Models.Response.WebAuthn;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Fido2;
using Fido2NetLib;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

/// <summary>
/// Real-crypto coverage for WebAuthn passkey registration (attestation), mirroring
/// Identity.IntegrationTest's WebAuthnGrantValidatorTests coverage for assertion/login.
/// No Fido2NetLib mocking — FakeWebAuthnAuthenticator produces a genuine ECDSA-signed,
/// CBOR-encoded attestation that the real Fido2NetLib library must independently verify.
/// </summary>
public class WebAuthnControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _rpId = "localhost";
    private const string _origin = "https://localhost:8080";
    private const string _masterPasswordHash = "master_password_hash";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IWebAuthnCredentialRepository _credentialRepository;
    private readonly IUserRepository _userRepository;

    private string _email = null!;

    public WebAuthnControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _credentialRepository = _factory.GetService<IWebAuthnCredentialRepository>();
        _userRepository = _factory.GetService<IUserRepository>();
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_email, _masterPasswordHash);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Register_ValidAttestation_CreatesCredential()
    {
        await _loginHelper.LoginAsync(_email);
        using var authenticator = new FakeWebAuthnAuthenticator();

        var optionsResponse = await PostAttestationOptionsAsync();
        var attestation = authenticator.MakeAttestation(optionsResponse.Options.Challenge, _rpId, _origin);

        var response = await PostCredentialAsync(optionsResponse.Token, attestation);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected 200, got {response.StatusCode}. Body: {content}");

        // WebAuthnCredentialResponseModel only has a WebAuthnCredential-accepting constructor, so
        // System.Text.Json can't deserialize it directly (no property-matching ctor) — read the raw JSON instead.
        using var credentialResponse = JsonDocument.Parse(content);
        var responseName = credentialResponse.RootElement.GetProperty("name").GetString();
        var responseId = Guid.Parse(credentialResponse.RootElement.GetProperty("id").GetString()!);
        Assert.Equal("Test Passkey", responseName);

        var user = await _userRepository.GetByEmailAsync(_email);
        Assert.NotNull(user);
        var persistedCredentials = await _credentialRepository.GetManyByUserIdAsync(user.Id);
        var persisted = Assert.Single(persistedCredentials);
        Assert.Equal(responseId, persisted.Id);
        Assert.NotEmpty(persisted.CredentialId);
        Assert.NotEmpty(persisted.PublicKey);
    }

    [Fact]
    public async Task Register_TamperedAttestation_ThrowsBadRequestException()
    {
        await _loginHelper.LoginAsync(_email);
        using var authenticator = new FakeWebAuthnAuthenticator();

        var optionsResponse = await PostAttestationOptionsAsync();
        // Sign the attestation over a different (wrong) challenge than the one issued by the server,
        // simulating a tampered/invalid device response.
        var wrongChallenge = new byte[optionsResponse.Options.Challenge.Length];
        var attestation = authenticator.MakeAttestation(wrongChallenge, _rpId, _origin);

        var response = await PostCredentialAsync(optionsResponse.Token, attestation);

        Assert.False(response.IsSuccessStatusCode);

        var user = await _userRepository.GetByEmailAsync(_email);
        Assert.NotNull(user);
        var persistedCredentials = await _credentialRepository.GetManyByUserIdAsync(user.Id);
        Assert.Empty(persistedCredentials);
    }

    private async Task<WebAuthnCredentialCreateOptionsResponseModel> PostAttestationOptionsAsync()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/webauthn/attestation-options")
        {
            Content = JsonContent.Create(new SecretVerificationRequestModel
            {
                MasterPasswordHash = _masterPasswordHash,
            }),
        };
        var response = await _client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var options = await response.Content.ReadFromJsonAsync<WebAuthnCredentialCreateOptionsResponseModel>();
        Assert.NotNull(options);
        return options;
    }

    private async Task<HttpResponseMessage> PostCredentialAsync(
        string token,
        AuthenticatorAttestationRawResponse attestation)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/webauthn")
        {
            Content = JsonContent.Create(new WebAuthnLoginCredentialCreateRequestModel
            {
                DeviceResponse = attestation,
                Name = "Test Passkey",
                Token = token,
                SupportsPrf = false,
            }),
        };
        return await _client.SendAsync(message);
    }
}
