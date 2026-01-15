using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class CiphersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;
    private User _owner = null!;

    public CiphersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var userRepository = _factory.GetService<IUserRepository>();
        _owner = await userRepository.GetByEmailAsync(_ownerEmail);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeleteOrphanedCipher_AfterCollectionDeleted_Succeeds()
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory,
            _organization.Id,
            "Test Collection for Deletion");

        var cipherRepository = _factory.GetService<ICipherRepository>();
        var cipherDetails = new CipherDetails
        {
            Type = CipherType.Login,
            OrganizationId = _organization.Id,
            Data = "{\"Name\":\"Test Cipher\"}",
        };
        await cipherRepository.CreateAsync(cipherDetails, new List<Guid> { collection.Id });
        var cipher = cipherDetails;

        var cipherService = _factory.GetService<ICipherService>();
        await cipherService.SoftDeleteAsync(cipher, _owner.Id, orgAdmin: true);
        await collectionRepository.DeleteAsync(collection);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await cipherService.DeleteAsync(cipher, _owner.Id, orgAdmin: true);
        });

        Assert.Null(exception);
        var deletedCipher = await cipherRepository.GetByIdAsync(cipher.Id);
        Assert.Null(deletedCipher);
    }

    [Fact]
    public async Task RestoreOrphanedCipher_AfterCollectionDeleted_Succeeds()
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory,
            _organization.Id,
            "Test Collection for Restore");

        var cipherRepository = _factory.GetService<ICipherRepository>();
        var cipherDetails = new CipherDetails
        {
            Type = CipherType.Login,
            OrganizationId = _organization.Id,
            Data = "{\"Name\":\"Test Cipher for Restore\"}",
        };
        await cipherRepository.CreateAsync(cipherDetails, new List<Guid> { collection.Id });
        var cipher = cipherDetails;

        var cipherService = _factory.GetService<ICipherService>();
        await cipherService.SoftDeleteAsync(cipher, _owner.Id, orgAdmin: true);
        await collectionRepository.DeleteAsync(collection);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await cipherService.RestoreAsync(cipher, _owner.Id, orgAdmin: true);
        });

        Assert.Null(exception);
        var restoredCipher = await cipherRepository.GetByIdAsync(cipher.Id);
        Assert.NotNull(restoredCipher);
        Assert.Null(restoredCipher.DeletedDate);
    }

    [Fact]
    public async Task DeleteCipher_WithMultipleCollections_DeleteOneCollection_StillAccessible()
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var collection1 = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory,
            _organization.Id,
            "Collection 1");
        var collection2 = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory,
            _organization.Id,
            "Collection 2");

        var cipherRepository = _factory.GetService<ICipherRepository>();
        var cipherDetails = new CipherDetails
        {
            Type = CipherType.Login,
            OrganizationId = _organization.Id,
            Data = "{\"Name\":\"Test Cipher Multiple Collections\"}",
        };
        await cipherRepository.CreateAsync(cipherDetails, new List<Guid> { collection1.Id, collection2.Id });
        var cipher = cipherDetails;

        var cipherService = _factory.GetService<ICipherService>();
        await cipherService.SoftDeleteAsync(cipher, _owner.Id, orgAdmin: true);
        await collectionRepository.DeleteAsync(collection1);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await cipherService.DeleteAsync(cipher, _owner.Id, orgAdmin: true);
        });

        Assert.Null(exception);
        var deletedCipher = await cipherRepository.GetByIdAsync(cipher.Id);
        Assert.Null(deletedCipher);
    }
}
