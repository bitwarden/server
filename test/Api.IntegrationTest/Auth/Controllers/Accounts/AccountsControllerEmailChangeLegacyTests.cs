using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.EmailChangeTestHelpers;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;

namespace Bit.Api.IntegrationTest.Auth.Controllers.Accounts;

public class AccountsControllerEmailChangeLegacyTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IStripeSyncService _stripeSyncService;

    private string _ownerEmail = null!;

    public AccountsControllerEmailChangeLegacyTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(_ => { });
        _factory.SubstituteService<IStripeSyncService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _featureService = _factory.GetService<Bitwarden.Server.Sdk.Features.IFeatureService>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        _stripeSyncService = _factory.GetService<IStripeSyncService>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ===== Email change: legacy path (PM30806_SelfServiceChangeEmailCommand flag OFF) =====
    // These tests pin the flag OFF to exercise the legacy UserService.ChangeEmailAsync path,
    // which rotates the master password and wrapped user key as part of the email change. They
    // share the legacy-shaped PostEmailAsync helper at the end of this class.
    //
    // The class-scoped Bitwarden.Server.Sdk.Features.IFeatureService substitute leaks Returns(...) values across tests in this
    // class, so every test sets the flag explicitly rather than relying on a default.
    //
    // TODO: PM-39120 - On flag cleanup, delete this file (all four _SelfServiceFlagOff_ tests plus
    // the PostEmailAsync helper). The _SelfServiceFlagOn_ tests then become the canonical
    // email-change tests; see the note on them.
    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_UpdatesEmailAndPassword()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        // Act
        var response = await PostEmailAsync(newEmail, token);

        // Assert
        response.EnsureSuccessStatusCode();

        var updatedUser = await _userRepository.GetByEmailAsync(newEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.True(updatedUser.EmailVerified);
        Assert.Equal(MasterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword!, NewMasterPasswordHash));
        Assert.Equal(newEmail, updatedUser.MasterPasswordSalt);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_InvalidMasterPassword_BadRequest()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        var requestModel = new EmailRequestModel
        {
            MasterPasswordHash = "wrong_master_password_hash",
            NewEmail = newEmail,
            NewMasterPasswordHash = NewMasterPasswordHash,
            Token = token,
            Key = MasterKeyWrappedUserKey
        };

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify email was not changed
        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_StripeSyncFails_MasterPasswordSaltIsRolledBack()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        // Set up the user as a Stripe customer to exercise the sync code path in ChangeEmailAsync
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_test_stripe_fail";
        await _userRepository.ReplaceAsync(user);

        // Configure the substitute to simulate a Stripe sync failure after the DB write
        _stripeSyncService
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new Exception("Stripe sync failure")));

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        // Act
        var response = await PostEmailAsync(newEmail, token);

        // Assert - Stripe failure is surfaced as a bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify MasterPasswordSalt was rolled back to the original email, not left at newEmail.
        // ChangeEmailAsync sets MasterPasswordSalt = newEmail and persists before attempting Stripe
        // sync; on failure it must re-persist MasterPasswordSalt = previousState.Email.
        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.MasterPasswordSalt);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_MissingNewMasterPasswordHashAndKey_BadRequest()
    {
        // With the flag off, the legacy path still requires NewMasterPasswordHash and Key. The
        // self-service-only payload (no password rotation, no key) must be rejected so legacy
        // clients can't accidentally bypass key rotation.
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, token, MasterPasswordHash);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    private async Task<HttpResponseMessage> PostEmailAsync(string newEmail, string token)
    {
        var requestModel = new EmailRequestModel
        {
            MasterPasswordHash = MasterPasswordHash,
            NewEmail = newEmail,
            NewMasterPasswordHash = NewMasterPasswordHash,
            Token = token,
            Key = MasterKeyWrappedUserKey
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email");
        message.Content = JsonContent.Create(requestModel);
        return await _client.SendAsync(message);
    }
}
