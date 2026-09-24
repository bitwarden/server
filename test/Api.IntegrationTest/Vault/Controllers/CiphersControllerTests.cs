using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models.Request;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class CiphersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

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
        _ownerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        // Allow all admins to edit collections.
        _organization.AllowAdminAccessToAllCollectionItems = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(_organization);
        await _factory.GetService<IOrganizationAbilityCacheService>().UpsertOrganizationAbilityAsync(_organization, CancellationToken.None);

        // Turn on the Organization Data Ownership ("use my items") policy.
        await OrganizationTestHelpers.EnableOrganizationDataOwnershipPolicyAsync(_factory, _organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    public static IEnumerable<object[]> EditorRoles =>
    [
        [OrganizationUserType.Owner],
        [OrganizationUserType.Admin],
        [OrganizationUserType.Custom],
    ];

    [Theory]
    [MemberData(nameof(EditorRoles))]
    public async Task PostBulkCollections_CannotAssignCipherFromDefaultCollectionToSharedCollection_WhenItIsNotTheirMyItems(OrganizationUserType editorType)
    {
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();

        var (editorEmail, cipherId, defaultCollectionId, sharedCollectionId) = await ArrangeCipherInDefaultCollectionAsync(editorType);

        // Act: the member assigns the cipher to the shared collection.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PostAsJsonAsync("ciphers/bulk-collections", new CipherBulkUpdateCollectionsRequestModel
        {
            OrganizationId = _organization.Id,
            CipherIds = [cipherId],
            CollectionIds = [sharedCollectionId],
            RemoveCollections = false
        });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
        Assert.DoesNotContain(sharedCollectionId, collectionIds);
        Assert.Contains(defaultCollectionId, collectionIds);
    }

    [Theory]
    [MemberData(nameof(EditorRoles))]
    public async Task PostBulkCollections_CannotRemoveCipherFromAnotherUsersDefaultCollection_WhenTheyDoNotOwnIt(OrganizationUserType editorType)
    {
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();

        var (editorEmail, cipherId, defaultCollectionId, sharedCollectionId) =
            await ArrangeCipherInDefaultCollectionAsync(editorType);

        // The cipher is tied to both the default ("My Items") collection and a shared collection.
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            _organization.Id, [cipherId], [sharedCollectionId]);

        // Act: the member removes the cipher from another member's default ("My Items") collection.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PostAsJsonAsync("ciphers/bulk-collections", new CipherBulkUpdateCollectionsRequestModel
        {
            OrganizationId = _organization.Id,
            CipherIds = [cipherId],
            CollectionIds = [defaultCollectionId],
            RemoveCollections = true
        });

        // Assert: the request is rejected and the cipher's collections are unchanged.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
        Assert.Contains(defaultCollectionId, collectionIds);
        Assert.Contains(sharedCollectionId, collectionIds);
    }

    [Theory]
    [MemberData(nameof(EditorRoles))]
    public async Task PostBulkCollections_AddsCipherToSharedCollectionWithoutExplicitAccess(OrganizationUserType editorType)
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var cipherRepository = _factory.GetService<ICipherRepository>();
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();

        // Custom members need the EditAnyCollection permission; Owners/Admins rely on the org setting.
        var permissions = editorType == OrganizationUserType.Custom
            ? new Permissions { EditAnyCollection = true }
            : null;

        var (editorEmail, editor) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, editorType, permissions);

        // Another member exclusively owns the shared collection; the acting member is not assigned to it.
        var (_, collectionOwner) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var sharedCollection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory, _organization.Id, "Someone Else's Shared Collection",
            users: [new CollectionAccessSelection { Id = collectionOwner.Id, ReadOnly = false, HidePasswords = false, Manage = true }]);

        // The acting member has no explicit access to the shared collection.
        var editorCollections = await collectionRepository.GetManyByUserIdAsync(editor.UserId!.Value);
        Assert.DoesNotContain(editorCollections, c => c.Id == sharedCollection.Id);

        var cipher = new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = _organization.Id,
            Data = "{}"
        };
        await cipherRepository.CreateAsync(cipher, Array.Empty<Guid>());

        // Act: the member adds the cipher to a shared collection they don't have explicit access to.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PostAsJsonAsync("ciphers/bulk-collections", new CipherBulkUpdateCollectionsRequestModel
        {
            OrganizationId = _organization.Id,
            CipherIds = [cipher.Id],
            CollectionIds = [sharedCollection.Id],
            RemoveCollections = false
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipher.Id);
        Assert.Contains(sharedCollection.Id, collectionIds);
    }

    /// <summary>
    /// Creates the acting member with the given role, a second member who owns a default user collection
    /// ("My Items") containing an org-owned cipher, and a shared collection.
    /// </summary>
    private async Task<(string editorEmail, Guid cipherId, Guid defaultCollectionId, Guid sharedCollectionId)> ArrangeCipherInDefaultCollectionAsync(OrganizationUserType editorType)
    {
        var collectionRepository = _factory.GetService<ICollectionRepository>();
        var cipherRepository = _factory.GetService<ICipherRepository>();

        // Custom members need the EditAnyCollection permission; Owners/Admins rely on the org setting.
        var permissions = editorType == OrganizationUserType.Custom
            ? new Permissions { EditAnyCollection = true }
            : null;

        var (editorEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, editorType, permissions);

        // A regular member who owns a default user collection ("My Items") with a cipher in it.
        var (_, itemOwner) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var defaultCollection = new Collection
        {
            OrganizationId = _organization.Id,
            Name = "My Items",
            Type = CollectionType.DefaultUserCollection
        };
        await collectionRepository.CreateAsync(defaultCollection, null, [new CollectionAccessSelection
            { Id = itemOwner.Id, ReadOnly = false, HidePasswords = false, Manage = true }
        ]);

        var cipher = new Cipher
        {
            Type = CipherType.Login,
            OrganizationId = _organization.Id,
            Data = "{}"
        };
        await cipherRepository.CreateAsync(cipher, [defaultCollection.Id]);

        var sharedCollection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory, _organization.Id, "Shared Collection");

        return (editorEmail, cipher.Id, defaultCollection.Id, sharedCollection.Id);
    }
}
