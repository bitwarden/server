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
    private const string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

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
    public async Task PostBulkCollections_CanRemoveCipherFromSharedCollection_WhenItIsAlsoInAnotherUsersDefaultCollection(OrganizationUserType editorType)
    {
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();

        var (editorEmail, cipherId, defaultCollectionId, sharedCollectionId) =
            await ArrangeCipherInDefaultCollectionAsync(editorType);

        // The cipher is tied to both another member's default ("My Items") collection and a shared
        // collection the editor manages.
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            _organization.Id, [cipherId], [sharedCollectionId]);

        // Act: the editor removes the cipher from the shared collection only, leaving the default
        // ("My Items") collection untouched.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PostAsJsonAsync("ciphers/bulk-collections", new CipherBulkUpdateCollectionsRequestModel
        {
            OrganizationId = _organization.Id,
            CipherIds = [cipherId],
            CollectionIds = [sharedCollectionId],
            RemoveCollections = true
        });

        // Assert: the removal succeeds; the shared collection is gone and the default is untouched.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
        Assert.DoesNotContain(sharedCollectionId, collectionIds);
        Assert.Contains(defaultCollectionId, collectionIds);
    }

    [Theory]
    [MemberData(nameof(EditorRoles))]
    public async Task PutCollections_CanRemoveCipherFromSharedCollection_WhenItIsAlsoInAnotherUsersDefaultCollection(OrganizationUserType editorType)
    {
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();
        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        var (editorEmail, cipherId, defaultCollectionId, sharedCollectionId) =
            await ArrangeCipherInDefaultCollectionAsync(editorType);

        // A second shared collection the editor manages, so the editor keeps access to the cipher
        // after removing it from the first shared collection.
        var editorUser = await userRepository.GetByEmailAsync(editorEmail);
        var editorOrgUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, editorUser!.Id);
        var secondSharedCollection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory, _organization.Id, "Second Shared Collection",
            users: [new CollectionAccessSelection { Id = editorOrgUser!.Id, ReadOnly = false, HidePasswords = false, Manage = true }]);

        // The cipher is tied to another member's default ("My Items") collection and both shared collections.
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            _organization.Id, [cipherId], [sharedCollectionId, secondSharedCollection.Id]);

        // Act: the editor saves a collection list that drops the first shared collection. The owner's
        // default ("My Items") collection is outside their scope and is left untouched.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PutAsJsonAsync($"ciphers/{cipherId}/collections", new CipherCollectionsRequestModel
        {
            CollectionIds = [secondSharedCollection.Id.ToString()]
        });

        // Assert: the removal succeeds; the first shared collection is gone and the default is untouched.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
        Assert.DoesNotContain(sharedCollectionId, collectionIds);
        Assert.Contains(secondSharedCollection.Id, collectionIds);
        Assert.Contains(defaultCollectionId, collectionIds);
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

    [Theory]
    [MemberData(nameof(EditorRoles))]
    public async Task Put_CanEditCipherContents_WhenItIsAlsoInAnotherUsersDefaultCollection(OrganizationUserType editorType)
    {
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();

        var (editorEmail, cipherId, defaultCollectionId, sharedCollectionId) =
            await ArrangeCipherInDefaultCollectionAsync(editorType);

        // The cipher lives in both another member's default ("My Items") collection and a shared collection
        // the editor can reach. This mirrors an item shared under the Organization Data Ownership policy.
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            _organization.Id, [cipherId], [sharedCollectionId]);

        // Act: the editor saves a content change without touching the cipher's collections.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PutAsJsonAsync($"ciphers/{cipherId}", new CipherRequestModel
        {
            Type = CipherType.Login,
            OrganizationId = _organization.Id.ToString(),
            Name = _mockEncryptedString,
            Data = "{}"
        });

        // Assert: a normal edit succeeds and the collections are unchanged.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
        Assert.Contains(defaultCollectionId, collectionIds);
        Assert.Contains(sharedCollectionId, collectionIds);
    }

    [Theory]
    [MemberData(nameof(EditorRoles))]
    public async Task PutCollections_CanReSaveUnchangedCollections_WhenCipherIsAlsoInAnotherUsersDefaultCollection(OrganizationUserType editorType)
    {
        var collectionCipherRepository = _factory.GetService<ICollectionCipherRepository>();

        var (editorEmail, cipherId, defaultCollectionId, sharedCollectionId) =
            await ArrangeCipherInDefaultCollectionAsync(editorType);

        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(
            _organization.Id, [cipherId], [sharedCollectionId]);

        // Act: the editor re-saves the collection they can already see, making no change.
        await _loginHelper.LoginAsync(editorEmail);

        var response = await _client.PutAsJsonAsync($"ciphers/{cipherId}/collections", new CipherCollectionsRequestModel
        {
            CollectionIds = [sharedCollectionId.ToString()]
        });

        // Assert: the no-op collection save succeeds and the collections are unchanged.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var collectionIds = await collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
        Assert.Contains(defaultCollectionId, collectionIds);
        Assert.Contains(sharedCollectionId, collectionIds);
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

        var (editorEmail, editor) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
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

        // The editor has explicit Manage access to the shared collection only.
        var sharedCollection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory, _organization.Id, "Shared Collection",
            users: [new CollectionAccessSelection { Id = editor.Id, ReadOnly = false, HidePasswords = false, Manage = true }]);

        return (editorEmail, cipher.Id, defaultCollection.Id, sharedCollection.Id);
    }
}
