﻿using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Bit.Api.IntegrationTest.KeyManagement.Controllers;

public class AccountsKeyManagementControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private string _ownerEmail = null!;

    public AccountsKeyManagementControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.UpdateConfiguration("globalSettings:launchDarkly:flagValues:pm-12241-private-key-regeneration",
            "true");
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _emergencyAccessRepository = _factory.GetService<IEmergencyAccessRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
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

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_FeatureFlagTurnedOff_NotFound(KeyRegenerationRequestModel request)
    {
        // Localize factory to inject a false value for the feature flag.
        var localFactory = new ApiApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:launchDarkly:flagValues:pm-12241-private-key-regeneration",
            "false");
        var localClient = localFactory.CreateClient();
        var localEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var localLoginHelper = new LoginHelper(localFactory, localClient);
        await localFactory.LoginWithNewAccount(localEmail);
        await localLoginHelper.LoginAsync(localEmail);

        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await localClient.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_NotLoggedIn_Unauthorized(KeyRegenerationRequestModel request)
    {
        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await _client.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Confirmed, EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(OrganizationUserStatusType.Revoked, EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Revoked, EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(OrganizationUserStatusType.Revoked, EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, null)]
    [BitAutoData(OrganizationUserStatusType.Revoked, null)]
    [BitAutoData(OrganizationUserStatusType.Invited, EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Invited, EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(OrganizationUserStatusType.Invited, EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task RegenerateKeysAsync_UserInOrgOrHasDesignatedEmergencyAccess_ThrowsBadRequest(
        OrganizationUserStatusType organizationUserStatus,
        EmergencyAccessStatusType? emergencyAccessStatus,
        KeyRegenerationRequestModel request)
    {
        if (organizationUserStatus is OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked)
        {
            await CreateOrganizationUserAsync(organizationUserStatus);
        }

        if (emergencyAccessStatus != null)
        {
            await CreateDesignatedEmergencyAccessAsync(emergencyAccessStatus.Value);
        }

        await _loginHelper.LoginAsync(_ownerEmail);
        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await _client.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_Success(KeyRegenerationRequestModel request)
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await _client.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.Equal(request.UserPublicKey, user.PublicKey);
        Assert.Equal(request.UserKeyEncryptedUserPrivateKey, user.PrivateKey);
    }

    private async Task CreateOrganizationUserAsync(OrganizationUserStatusType organizationUserStatus)
    {
        var (_, organizationUser) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        organizationUser.Status = organizationUserStatus;
        await _organizationUserRepository.ReplaceAsync(organizationUser);
    }

    private async Task CreateDesignatedEmergencyAccessAsync(EmergencyAccessStatusType emergencyAccessStatus)
    {
        var tempEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(tempEmail);

        var tempUser = await _userRepository.GetByEmailAsync(tempEmail);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = tempUser!.Id,
            GranteeId = user!.Id,
            KeyEncrypted = _mockEncryptedString,
            Status = emergencyAccessStatus,
            Type = EmergencyAccessType.View,
            WaitTimeDays = 10,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
        await _emergencyAccessRepository.CreateAsync(emergencyAccess);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeysAsync_NotLoggedIn_Unauthorized(RotateUserAccountKeysAndDataRequestModel request)
    {
        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeysAsync_Success(RotateUserAccountKeysAndDataRequestModel request)
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var password = _passwordHasher.HashPassword(user, "newMasterPassword");
        user.MasterPassword = password;
        user.PublicKey = "publicKey";
        await _userRepository.ReplaceAsync(user);

        request.AccountUnlockData.MasterPasswordUnlockData.KdfType = user.Kdf;
        request.AccountUnlockData.MasterPasswordUnlockData.KdfIterations = user.KdfIterations;
        request.AccountUnlockData.MasterPasswordUnlockData.KdfMemory = user.KdfMemory;
        request.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism = user.KdfParallelism;
        request.AccountUnlockData.MasterPasswordUnlockData.Email = user.Email;
        request.AccountKeys.AccountPublicKey = "publicKey";
        request.AccountKeys.UserKeyEncryptedAccountPrivateKey = _mockEncryptedString;

        request.OldMasterKeyAuthenticationHash = "newMasterPassword";

        request.AccountData.Ciphers =
        [
            new CipherWithIdRequestModel
            {
                Id = Guid.NewGuid(),
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString,
                },
            },
        ];
        request.AccountData.Folders = [
            new FolderWithIdRequestModel
            {
                Id = Guid.NewGuid(),
                Name = _mockEncryptedString,
            },
        ];
        request.AccountData.Sends = [
            new SendWithIdRequestModel
            {
                Id = Guid.NewGuid(),
                Name = _mockEncryptedString,
                Key = _mockEncryptedString,
                Disabled = false,
                DeletionDate = DateTime.UtcNow.AddDays(1),
            },
        ];
        request.AccountUnlockData.MasterPasswordUnlockData.MasterKeyEncryptedUserKey = _mockEncryptedString;
        request.AccountUnlockData.PasskeyUnlockData = [];
        request.AccountUnlockData.EmergencyAccessUnlockData = [];
        request.AccountUnlockData.OrganizationAccountRecoveryUnlockData = [];

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.Email, userNewState.Email);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfType, userNewState.Kdf);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfIterations, userNewState.KdfIterations);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfMemory, userNewState.KdfMemory);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism, userNewState.KdfParallelism);
    }
}
