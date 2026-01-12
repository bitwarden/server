using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Integration tests for the AcceptInit endpoint (POST /organizations/{orgId}/users/{organizationUserId}/accept-init).
/// This endpoint is used when a user accepts an invitation to a pending organization, initializing the organization
/// by setting its keys and status, accepting the user's invitation, and confirming the user as a member.
/// </summary>
public class OrganizationUsersControllerAcceptInitTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _mockPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwMj7W00xS7H0NWasGn7PfEq8VfH3fa5XuZucsKxLLRAHHZk0xGRZJH2lFIznizv3GpF8vzhHhe9VpmMkrdIa5oWhwHpy+D7Z1QCQxuUXzvMKpa95GOntr89nN/mWKpk6abjgjmDcqFJ0lhDqkKnDfes+d8BBd5oEA8p41/Ykz7OfG7AiktVBpTQFW09MQh1NOvcLxVgiUUVRPwNRKrOeCekWDtOjZhASMETv3kI1ogvhHukOQ3ztDzrxvmwnLQ+cXl1EeD8gQnGDp3QLiJqxPgh2EdmANh4IzjRexoDn6BqhRGqLLIoLAbbkoiNrd6NYujrWW0N8KMMoVEXuJL2g4wIDAQAB";
    private const string _mockEncryptedPrivateKey = "2.Ytudv+Qk3ET9hN8whqpuGg==|ijsFhmjaf1aaT9uz+IPhVTzMS+2W/ldAP8LdT5VyJaFdx4HSdLcWSZvz5xWuuW94zfv1Qh+p3iQIuZOr29G4jcx47rYtz4ssiFtB7Ia552ZeF+cb7uuVg40CIe7ycuJQITk00o8gots+wFnaEvk0Vjgycnqutm0jpeBJ1joWJWqTVgSsYdUGLu7PiJywQ9NgY4+bJXqadlcviS3rhPKJXtiXYJhqJqSw+vI0Yxp96MJ0HcFJk/LG22YJPTvL5kzuDq/Wzj40kj8blQ+ag+xHD4P/KJ/MppEB3OpDw3UoJ50Ek+YB9pOqGxZtvqMEzBDsgh0yoz1O992UnhaUqtJ5e9Bxy3PA6cJsdyn9npduNOreEb8vePCidN2XC+chjJpPFpjms9muHLKgfaTIfpiJA2Tz8E9dvSyhHHTE1mY+xEA7P08BYKN3LNoSGIjdiZuouJ1V/KZvCssDfVG1tli2qpnhTIh4m3rAMhbM8WW3B7wCV8N0MpcJJSvndkVcMgRbgWcbivLeXuKdE/K98n01RvOLSJyslhLGCGEQQKw6N3HQ2iELfv84YQZi2fjDK+OqAmXDq1pNcjKX2I8dqBwl31tPC8qSZiWnfinwLdqQTvSQjOIyAHb4sSjAwgdMbCRzUTChRr09l+PAZqGWdMC5N2Bw+bA8WP0l2Wdxuv9Abxl3F7xGeAA9Rw9PU5wGKujaMRmO4V9MFjNyyCcw4D9pzKMW6OUKsHsHE7tsG7KskCzksHzrZGawAt0S41BYQA/JwePCrD3F6dM92anlC1LfA00KJb0tmFdU0yJNmJfR+S78yn8yM6wDgIs2cFB3W1fYfpfUvQm+zzPoEQihNxBxnwFsBtMAOtPy54FjSzKmxsQTrYT9E6NFb8k6ZIIm2gNeOPK9OUJgjw+4g2BXErM6ikHTzM3xcaTq/cQaePZ52emndw1qOtdV06hr2EeuLM8frfLHpsknUe8JeYeW5p9E8QdZjjSN9034usdYNamUdxzmn/Mw/ar8z1xSKS6zcaQoTQ7aYLEX3dWJndc4W64HyiaRkLjO6qLUFeOerfz5UvcxxRY89eAA0KLC2xnGkBMOhXxYzIB3lF8Zxqb4JMhoBGw1n31TDfhRDGDHHEAsZuAIcH7aC5RDVxU08Jxmw4oLmeTDZA5BFcqp2A3fusNVZUnfpmMy6DCJyFprlRl8jSlJMAvhbxVuuLFDZnjl77Z2of796Ur6DgmNwYtMPNEntZPIcZ76VPLWAL8lqiRBm20c4qiwr5rNSr5kry9bR1EfXHwFRjy5pxFQ+5+ilpRl8WPfT/iUuORd8J2wnCmghm7uxiJd9t82kX0s6benhL29dQ1etqt5soX2RnlfKan16GVWoI3xrljIQrCAY4xpdptSpglOnrpSClbN1nhGkDfFPNq2pWhQrDbznDknAJ9MxQaVnLYPhn7I849GMd7EvpSkydwQu7QXn9+H4jxn6UEntNGxcL0xkG+xippvZEe+HBvcDD40efDQW1bDbILLjPb4rNRx4d3xaQnVNaF7L33osm5LgfXAQSwHJiURdkU4zmhtPP4zn0br0OdFlR3mPcrkeNeSvs7FxiKtD6n6s+av+4bKjbLL1OyuwmTnMilL6p+m8ldte0yos/r+zOuxWeI=|euhiXWXehYbFQhlAV6LIECSIPCIRaHbNdr9OI4cTPUM=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private IFeatureService _featureService = null!;

    private Organization _pendingOrganization = null!;
    private User _invitedUser = null!;
    private OrganizationUser _invitedOrgUser = null!;
    private string _invitedUserEmail = null!;

    public OrganizationUsersControllerAcceptInitTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _featureService = _factory.GetService<IFeatureService>();
    }

    public async Task InitializeAsync()
    {
        // Create a pending organization without keys
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _pendingOrganization = new Organization
        {
            Name = "Pending Test Org",
            BillingEmail = $"{Guid.NewGuid()}@example.com",
            Plan = "Free",
            PlanType = PlanType.Free,
            Enabled = false,
            Status = OrganizationStatusType.Pending,
            PublicKey = null,
            PrivateKey = null,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
        await organizationRepository.CreateAsync(_pendingOrganization);

        // Create a user who will be invited to the pending organization
        _invitedUserEmail = $"{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_invitedUserEmail);

        var userRepository = _factory.GetService<IUserRepository>();
        _invitedUser = await userRepository.GetByEmailAsync(_invitedUserEmail);

        // Create organization user as invited (not yet accepted)
        // Note: UserId should be NULL for invited users who haven't accepted yet
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        _invitedOrgUser = new OrganizationUser
        {
            OrganizationId = _pendingOrganization.Id,
            UserId = null,  // NULL until they accept
            Email = _invitedUserEmail,
            Key = null,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Invited,
            AccessSecretsManager = false
        };
        await organizationUserRepository.CreateAsync(_invitedOrgUser);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AcceptInit_WithValidData_InitializesOrganizationAndConfirmsUser()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(false);

        await _loginHelper.LoginAsync(_invitedUserEmail);

        var token = GenerateInviteToken(_invitedOrgUser, _invitedUser.Email);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = token,
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify organization was initialized
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrganization = await organizationRepository.GetByIdAsync(_pendingOrganization.Id);

        Assert.NotNull(updatedOrganization);
        Assert.True(updatedOrganization.Enabled);
        Assert.Equal(OrganizationStatusType.Created, updatedOrganization.Status);
        Assert.Equal(_mockPublicKey, updatedOrganization.PublicKey);
        Assert.Equal(_mockEncryptedPrivateKey, updatedOrganization.PrivateKey);

        // Verify user was confirmed
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedOrgUser = await organizationUserRepository.GetByIdAsync(_invitedOrgUser.Id);

        Assert.NotNull(confirmedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedOrgUser.Status);
        Assert.Equal("test-user-key", confirmedOrgUser.Key);

        // Verify default collection was created
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(_pendingOrganization.Id);

        Assert.Single(collections);
        Assert.Equal(_mockEncryptedString, collections.First().Name);
        Assert.Equal(_pendingOrganization.Id, collections.First().OrganizationId);

        // Verify user has access to the collection
        var (_, collectionAccess) = await organizationUserRepository.GetByIdWithCollectionsAsync(_invitedOrgUser.Id);
        Assert.Single(collectionAccess);
        Assert.Equal(collections.First().Id, collectionAccess.First().Id);
        Assert.True(collectionAccess.First().Manage);
        Assert.False(collectionAccess.First().ReadOnly);
        Assert.False(collectionAccess.First().HidePasswords);
    }

    [Fact]
    public async Task AcceptInit_WithoutCollectionName_InitializesOrganizationWithoutCreatingCollection()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(false);

        await _loginHelper.LoginAsync(_invitedUserEmail);

        var token = GenerateInviteToken(_invitedOrgUser, _invitedUser.Email);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = token,
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = null
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify organization was initialized
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrganization = await organizationRepository.GetByIdAsync(_pendingOrganization.Id);

        Assert.NotNull(updatedOrganization);
        Assert.True(updatedOrganization.Enabled);
        Assert.Equal(OrganizationStatusType.Created, updatedOrganization.Status);

        // Verify user was confirmed
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedOrgUser = await organizationUserRepository.GetByIdAsync(_invitedOrgUser.Id);

        Assert.NotNull(confirmedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedOrgUser.Status);

        // Verify NO collection was created
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(_pendingOrganization.Id);

        Assert.Empty(collections);
    }

    [Fact]
    public async Task AcceptInit_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(false);

        await _loginHelper.LoginAsync(_invitedUserEmail);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = "invalid-token",
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify organization was NOT initialized
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(_pendingOrganization.Id);

        Assert.False(organization.Enabled);
        Assert.Equal(OrganizationStatusType.Pending, organization.Status);
        Assert.Null(organization.PublicKey);
        Assert.Null(organization.PrivateKey);
    }

    [Fact]
    public async Task AcceptInit_WithAlreadyEnabledOrganization_ReturnsBadRequest()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(false);

        // Update the organization to be already enabled
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _pendingOrganization.Enabled = true;
        _pendingOrganization.Status = OrganizationStatusType.Created;
        await organizationRepository.ReplaceAsync(_pendingOrganization);

        await _loginHelper.LoginAsync(_invitedUserEmail);

        var token = GenerateInviteToken(_invitedOrgUser, _invitedUser.Email);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = token,
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInit_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(false);

        // Don't log in
        var token = GenerateInviteToken(_invitedOrgUser, _invitedUser.Email);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = token,
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private string GenerateInviteToken(OrganizationUser orgUser, string email)
    {
        var tokenFactory = _factory.GetService<IDataProtectorTokenFactory<OrgUserInviteTokenable>>();

        var tokenable = new OrgUserInviteTokenable(orgUser);
        return tokenFactory.Protect(tokenable);
    }

    [Fact]
    public async Task AcceptInit_WithFeatureFlagEnabled_AtomicallyInitializesOrgAndConfirmsUser()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(true);

        await _loginHelper.LoginAsync(_invitedUserEmail);

        var token = GenerateInviteToken(_invitedOrgUser, _invitedUser.Email);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = token,
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify organization was initialized
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var updatedOrganization = await organizationRepository.GetByIdAsync(_pendingOrganization.Id);

        Assert.NotNull(updatedOrganization);
        Assert.True(updatedOrganization.Enabled);
        Assert.Equal(OrganizationStatusType.Created, updatedOrganization.Status);
        Assert.Equal(_mockPublicKey, updatedOrganization.PublicKey);
        Assert.Equal(_mockEncryptedPrivateKey, updatedOrganization.PrivateKey);

        // Verify user was confirmed (not just accepted)
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var confirmedOrgUser = await organizationUserRepository.GetByIdAsync(_invitedOrgUser.Id);

        Assert.NotNull(confirmedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, confirmedOrgUser.Status);
        Assert.Equal("test-user-key", confirmedOrgUser.Key);
        Assert.Equal(_invitedUser.Id, confirmedOrgUser.UserId);
        Assert.Null(confirmedOrgUser.Email);

        // Verify default collection was created
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(_pendingOrganization.Id);

        Assert.Single(collections);
        Assert.Equal(_mockEncryptedString, collections.First().Name);
    }

    [Fact]
    public async Task AcceptInit_WithFeatureFlagEnabled_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.RefactorOrgAcceptInit).Returns(true);

        await _loginHelper.LoginAsync(_invitedUserEmail);

        var acceptInitRequest = new OrganizationUserAcceptInitRequestModel
        {
            Token = "invalid-token",
            Key = "test-user-key",
            Keys = new OrganizationKeysRequestModel
            {
                PublicKey = _mockPublicKey,
                EncryptedPrivateKey = _mockEncryptedPrivateKey
            },
            CollectionName = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"organizations/{_pendingOrganization.Id}/users/{_invitedOrgUser.Id}/accept-init",
            acceptInitRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify NO state changes occurred (atomic behavior)
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(_pendingOrganization.Id);

        Assert.False(organization.Enabled);
        Assert.Equal(OrganizationStatusType.Pending, organization.Status);
        Assert.Null(organization.PublicKey);
        Assert.Null(organization.PrivateKey);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByIdAsync(_invitedOrgUser.Id);

        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Null(orgUser.UserId);
    }
}
