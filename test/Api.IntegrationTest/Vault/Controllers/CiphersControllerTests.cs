using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class CiphersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public CiphersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create organization with Enterprise plan (required for policies)
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostCreate_PersonalCipher_Success()
    {
        // Arrange
        await _loginHelper.LoginAsync(_ownerEmail);

        var cipherRequest = new CipherCreateRequestModel
        {
            Cipher = new CipherRequestModel
            {
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ciphers/create", cipherRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostCreate_OrganizationCipher_WithDefaultUserCollectionOwner_Success()
    {
        // Arrange
        var policyRepository = _factory.GetService<IPolicyRepository>();
        var policy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = "{}"
        };
        await policyRepository.UpsertAsync(policy);

        var memberEmail = $"member-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberEmail);

        var memberUser = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Accepted);

        // Confirm the member user which should trigger DefaultUserCollection creation
        var confirmOrganizationUserCommand = _factory.GetService<IConfirmOrganizationUserCommand>();
        var ownerUserRepository = _factory.GetService<IUserRepository>();
        var owner = await ownerUserRepository.GetByEmailAsync(_ownerEmail);

        await confirmOrganizationUserCommand.ConfirmUserAsync(
            _organization.Id,
            memberUser.Id,
            "test-key",
            owner!.Id,
            "My Collection");

        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByUserIdAsync(memberUser!.UserId!.Value);
        var defaultUserCollection = collections.FirstOrDefault(c => c.Type == CollectionType.DefaultUserCollection);

        Assert.NotNull(defaultUserCollection);
        Assert.Equal("My Collection", defaultUserCollection.Name);

        await _loginHelper.LoginAsync(memberEmail);

        // Create cipher in the DefaultUserCollection
        var cipherRequest = new CipherCreateRequestModel
        {
            Cipher = new CipherRequestModel
            {
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString
                },
                OrganizationId = _organization.Id.ToString()
            },
            CollectionIds = new[] { defaultUserCollection.Id }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ciphers/create", cipherRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cipherRepository = _factory.GetService<ICipherRepository>();
        var userCiphers = await cipherRepository.GetManyByUserIdAsync(memberUser.UserId!.Value, withOrganizations: true);
        var createdCipher = userCiphers.FirstOrDefault(c => c.OrganizationId == _organization.Id);

        Assert.NotNull(createdCipher);
        Assert.Equal(_organization.Id, createdCipher.OrganizationId);

        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();
        var collectionCiphers = await collectionCipherRepository.GetManyByOrganizationIdAsync(_organization.Id);
        Assert.Contains(collectionCiphers, cc => cc.CipherId == createdCipher.Id && cc.CollectionId == defaultUserCollection.Id);
    }

    [Fact]
    public async Task PostCreate_OrganizationCipher_WithNonOrganizationMember_ReturnsNotFound()
    {
        // Arrange
        var nonMemberEmail = $"non-member-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(nonMemberEmail);
        await _loginHelper.LoginAsync(nonMemberEmail);

        var cipherRequest = new CipherCreateRequestModel
        {
            Cipher = new CipherRequestModel
            {
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString
                },
                OrganizationId = _organization.Id.ToString()
            },
            CollectionIds = new Guid[] { Guid.NewGuid() }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ciphers/create", cipherRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCreate_OrganizationCipher_WithoutCollectionAccess_ReturnsBadRequest()
    {
        // Arrange
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var restrictedCollection = new Collection
        {
            OrganizationId = _organization.Id,
            Name = "Restricted Collection"
        };
        await collectionRepository.CreateAsync(restrictedCollection);

        // Create a member user (but don't give them access to the collection)
        var memberEmail = $"member-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberEmail);

        var memberUser = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Confirmed);

        await _loginHelper.LoginAsync(memberEmail);

        var cipherRequest = new CipherCreateRequestModel
        {
            Cipher = new CipherRequestModel
            {
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString
                },
                OrganizationId = _organization.Id.ToString()
            },
            CollectionIds = new[] { restrictedCollection.Id }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ciphers/create", cipherRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Specified CollectionId does not exist on the specified Organization", errorContent);
    }

    [Fact]
    public async Task Put_PersonalCipher_Success()
    {
        // Arrange
        var cipherRepository = _factory.GetService<ICipherRepository>();
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(_ownerEmail);

        var cipher = new CipherDetails
        {
            Type = CipherType.Login,
            Data = "{\"Name\":\"" + _mockEncryptedString + "\",\"Username\":\"" + _mockEncryptedString + "\",\"Password\":\"" + _mockEncryptedString + "\"}",
            UserId = user.Id,
            OrganizationId = null
        };

        await cipherRepository.CreateAsync(cipher);

        await _loginHelper.LoginAsync(_ownerEmail);

        var updateRequest = new CipherRequestModel
        {
            Type = CipherType.Card,
            Name = _mockEncryptedString,
            Login = new CipherLoginModel
            {
                Username = _mockEncryptedString,
                Password = _mockEncryptedString
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/ciphers/{cipher.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedCipher = await cipherRepository.GetByIdAsync(cipher.Id, user.Id);
        Assert.NotNull(updatedCipher);
        Assert.Null(updatedCipher.OrganizationId);
        Assert.True(updatedCipher.RevisionDate > cipher.RevisionDate);
    }

    [Fact]
    public async Task Put_OrganizationCipher_WithDefaultUserCollectionOwner_Success()
    {
        // Arrange
        var policyRepository = _factory.GetService<IPolicyRepository>();
        var policy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = "{}"
        };
        await policyRepository.UpsertAsync(policy);

        var memberEmail = $"member-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberEmail);

        var memberUser = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Accepted);

        var confirmOrganizationUserCommand = _factory.GetService<IConfirmOrganizationUserCommand>();
        var ownerUserRepository = _factory.GetService<IUserRepository>();
        var owner = await ownerUserRepository.GetByEmailAsync(_ownerEmail);

        await confirmOrganizationUserCommand.ConfirmUserAsync(
            _organization.Id,
            memberUser.Id,
            "test-key",
            owner!.Id,
            "My Collection");

        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collections = await collectionRepository.GetManyByUserIdAsync(memberUser!.UserId!.Value);
        var defaultUserCollection = collections.FirstOrDefault(c => c.Type == CollectionType.DefaultUserCollection);

        Assert.NotNull(defaultUserCollection);

        await _loginHelper.LoginAsync(memberEmail);

        var cipherRepository = _factory.GetService<ICipherRepository>();
        var memberUserRepository = _factory.GetService<IUserRepository>();
        var memberUserEntity = await memberUserRepository.GetByEmailAsync(memberEmail);

        var cipher = new CipherDetails
        {
            Type = CipherType.Login,
            Data = "{\"Name\":\"" + _mockEncryptedString + "\",\"Username\":\"" + _mockEncryptedString + "\",\"Password\":\"" + _mockEncryptedString + "\"}",
            UserId = memberUserEntity.Id,
            OrganizationId = _organization.Id
        };

        await cipherRepository.CreateAsync(cipher, new[] { defaultUserCollection.Id });

        var updateRequest = new CipherRequestModel
        {
            Type = CipherType.Card,
            Name = _mockEncryptedString,
            Login = new CipherLoginModel
            {
                Username = _mockEncryptedString,
                Password = _mockEncryptedString
            },
            OrganizationId = _organization.Id.ToString()
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/ciphers/{cipher.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedCipher = await cipherRepository.GetByIdAsync(cipher.Id, memberUserEntity.Id);
        Assert.NotNull(updatedCipher);
        Assert.Equal(_organization.Id, updatedCipher.OrganizationId);
        Assert.True(updatedCipher.RevisionDate > cipher.RevisionDate);
    }

    [Fact]
    public async Task Put_OrganizationCipher_WithoutCollectionAccess_ReturnsNotFound()
    {
        // Arrange
        await _loginHelper.LoginAsync(_ownerEmail);

        var ownerUserRepository = _factory.GetService<IUserRepository>();
        var owner = await ownerUserRepository.GetByEmailAsync(_ownerEmail);
        var cipherRepository = _factory.GetService<ICipherRepository>();

        var cipher = new CipherDetails
        {
            Type = CipherType.Login,
            Data = "{\"Name\":\"" + _mockEncryptedString + "\",\"Username\":\"" + _mockEncryptedString + "\",\"Password\":\"" + _mockEncryptedString + "\"}",
            UserId = owner.Id,
            OrganizationId = _organization.Id
        };

        await cipherRepository.CreateAsync(cipher);

        var memberEmail = $"member-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberEmail);

        await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Confirmed);

        await _loginHelper.LoginAsync(memberEmail);

        var updateRequest = new CipherRequestModel
        {
            Type = CipherType.Login,
            Name = _mockEncryptedString,
            Login = new CipherLoginModel
            {
                Username = _mockEncryptedString,
                Password = _mockEncryptedString
            },
            OrganizationId = _organization.Id.ToString()
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/ciphers/{cipher.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCreate_OrganizationCipher_WhenAddingToOtherUsersDefaultUserCollection_ReturnsBadRequest()
    {
        // Arrange
        var policyRepository = _factory.GetService<IPolicyRepository>();
        var policy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = "{}"
        };
        await policyRepository.UpsertAsync(policy);

        var memberAEmail = $"{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberAEmail);

        var memberA = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberAEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Accepted);

        var memberBEmail = $"{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberBEmail);

        var memberB = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberBEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Accepted);

        // Confirm both users to create their DefaultUserCollections
        var confirmOrganizationUserCommand = _factory.GetService<IConfirmOrganizationUserCommand>();
        var ownerUserRepository = _factory.GetService<IUserRepository>();
        var owner = await ownerUserRepository.GetByEmailAsync(_ownerEmail);

        await confirmOrganizationUserCommand.ConfirmUserAsync(
            _organization.Id,
            memberA.Id,
            "test-key-A",
            owner!.Id,
            "Member A Collection");

        await confirmOrganizationUserCommand.ConfirmUserAsync(
            _organization.Id,
            memberB.Id,
            "test-key-B",
            owner!.Id,
            "Member B Collection");

        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var memberBCollections = await collectionRepository.GetManyByUserIdAsync(memberB!.UserId!.Value);
        var memberBDefaultCollection = memberBCollections.FirstOrDefault(c => c.Type == CollectionType.DefaultUserCollection);

        Assert.NotNull(memberBDefaultCollection);

        // Login as Member A and try to create cipher in Member B's DefaultUserCollection
        await _loginHelper.LoginAsync(memberAEmail);

        var cipherRequest = new CipherCreateRequestModel
        {
            Cipher = new CipherRequestModel
            {
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString
                },
                OrganizationId = _organization.Id.ToString()
            },
            CollectionIds = new[] { memberBDefaultCollection.Id }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/ciphers/create", cipherRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Specified CollectionId does not exist on the specified Organization", errorContent);
    }

    [Fact]
    public async Task Put_OrganizationCipher_WhenAddingToOtherUsersDefaultUserCollection_ReturnsBadRequest()
    {
        // Arrange
        var policyRepository = _factory.GetService<IPolicyRepository>();
        var policy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true,
            Data = "{}"
        };
        await policyRepository.UpsertAsync(policy);

        var memberAEmail = $"{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberAEmail);

        var memberA = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberAEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Accepted);

        var memberBEmail = $"{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(memberBEmail);

        var memberB = await OrganizationTestHelpers.CreateUserAsync(_factory,
            _organization.Id,
            memberBEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Accepted);

        var confirmOrganizationUserCommand = _factory.GetService<IConfirmOrganizationUserCommand>();
        var ownerUserRepository = _factory.GetService<IUserRepository>();
        var owner = await ownerUserRepository.GetByEmailAsync(_ownerEmail);

        await confirmOrganizationUserCommand.ConfirmUserAsync(
            _organization.Id,
            memberA.Id,
            "test-key-A",
            owner!.Id,
            "Member A Collection");

        await confirmOrganizationUserCommand.ConfirmUserAsync(
            _organization.Id,
            memberB.Id,
            "test-key-B",
            owner!.Id,
            "Member B Collection");

        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var memberBCollections = await collectionRepository.GetManyByUserIdAsync(memberB!.UserId!.Value);
        var memberBDefaultCollection = memberBCollections.FirstOrDefault(c => c.Type == CollectionType.DefaultUserCollection);

        Assert.NotNull(memberBDefaultCollection);

        // Create cipher in Member B's DefaultUserCollection (as Member B)
        var cipherRepository = _factory.GetService<ICipherRepository>();
        var memberBUserRepository = _factory.GetService<IUserRepository>();
        var memberBUserEntity = await memberBUserRepository.GetByEmailAsync(memberBEmail);

        var cipher = new CipherDetails
        {
            Type = CipherType.Login,
            Data = "{\"Name\":\"" + _mockEncryptedString + "\",\"Username\":\"" + _mockEncryptedString + "\",\"Password\":\"" + _mockEncryptedString + "\"}",
            UserId = memberBUserEntity.Id,
            OrganizationId = _organization.Id
        };

        await cipherRepository.CreateAsync(cipher, new[] { memberBDefaultCollection.Id });

        await _loginHelper.LoginAsync(memberAEmail);

        var updateRequest = new CipherRequestModel
        {
            Type = CipherType.Card,
            Name = _mockEncryptedString,
            Login = new CipherLoginModel
            {
                Username = _mockEncryptedString,
                Password = _mockEncryptedString
            },
            OrganizationId = _organization.Id.ToString()
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/ciphers/{cipher.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
