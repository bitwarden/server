using System.Security.Claims;
using AutoFixture;
using Bit.Api.Models.Request;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Tools.ImportFeatures.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;
using ImportCiphersLimitationSettings = Bit.Core.Settings.GlobalSettings.ImportCiphersLimitationSettings;

namespace Bit.Api.Test.Tools.Controllers;

[ControllerCustomize(typeof(ImportCiphersController))]
[SutProviderCustomize]
public class ImportCiphersControllerTests
{
    private readonly ImportCiphersLimitationSettings _organizationCiphersLimitations = new()
    {
        CiphersLimit = 40000,
        CollectionRelationshipsLimit = 80000,
        CollectionsLimit = 2000
    };

    /*************************
     * PostImport - Individual
     *************************/
    [Theory, BitAutoData]
    public async Task PostImportIndividual_ImportCiphersRequestModel_BadRequestException(SutProvider<ImportCiphersController> sutProvider, IFixture fixture)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        var ciphers = fixture.CreateMany<CipherRequestModel>(7001).ToArray();
        var model = new ImportCiphersRequestModel
        {
            Ciphers = ciphers,
            FolderRelationships = null,
            Folders = null
        };

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostImport(model));

        // Assert
        Assert.Equal("You cannot import this much data at once.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PostImportIndividual_ImportCiphersRequestModel_Success(User user,
        IFixture fixture, SutProvider<ImportCiphersController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
                .With(c => c.ArchivedDate, (DateTime?)null)
                .CreateMany(1).ToArray())
            .Create();

        // Act
        await sutProvider.Sut.PostImport(request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received()
            .ImportIntoIndividualVaultAsync(
            Arg.Any<List<Folder>>(),
            Arg.Any<List<CipherDetails>>(),
            Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
            user.Id
            );
    }

    [Theory, BitAutoData]
    public async Task PostImportIndividual_WithArchivedDate_SavesArchivedDate(User user,
        IFixture fixture, SutProvider<ImportCiphersController> sutProvider)
    {
        var archivedDate = DateTime.UtcNow;
        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;

        sutProvider.GetDependency<Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.ArchivedDate, archivedDate)
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
                .CreateMany(1).ToArray())
            .Create();

        await sutProvider.Sut.PostImport(request);

        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received()
            .ImportIntoIndividualVaultAsync(
                Arg.Any<List<Folder>>(),
                Arg.Is<List<CipherDetails>>(ciphers => ciphers.First().ArchivedDate == archivedDate),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                user.Id
            );
    }

    /****************************
     * PostImport - Organization
     ****************************/

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ImportOrganizationCiphersRequestModel_BadRequestException(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        // Limits are set in appsettings.json, making values small for test to run faster.
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = new()
            {
                CiphersLimit = 4,
                CollectionRelationshipsLimit = 8,
                CollectionsLimit = 2
            };

        var userService = sutProvider.GetDependency<Bit.Core.Services.IUserService>();
        userService.GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(null as Guid?);

        var ciphers = fixture.CreateMany<CipherRequestModel>(5).ToArray();
        var model = new ImportOrganizationCiphersRequestModel
        {
            Collections = null,
            Ciphers = ciphers,
            CollectionRelationships = null
        };

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostImportOrganization(Arg.Any<string>(), model));

        // Assert
        Assert.Equal("You cannot import this much data at once.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ImportOrganizationCiphersRequestModel_Succeeds(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = "AD89E6F8-4E84-4CFE-A978-256CC0DBF974";
        var orgIdGuid = Guid.Parse(orgId);
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
                .With(c => c.ArchivedDate, (DateTime?)null)
                .CreateMany(1).ToArray())
            .With(y => y.Collections, fixture.Build<CollectionWithIdRequestModel>()
                .With(c => c.Id, orgIdGuid)
                .CreateMany(1).ToArray())
            .Create();

        // AccessImportExport permission setup
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Success());

        // BulkCollectionOperations.Create permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgIdGuid)
            .Returns(existingCollections.Select(c => new Collection { Id = orgIdGuid }).ToList());

        // Act
        await sutProvider.Sut.PostImportOrganization(orgId, request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received(1)
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_WithAccessImportExport_Succeeds(
    SutProvider<ImportCiphersController> sutProvider,
    IFixture fixture,
    User user)
    {
        // Arrange
        var orgId = "AD89E6F8-4E84-4CFE-A978-256CC0DBF974";
        var orgIdGuid = Guid.Parse(orgId);
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        var importCiphersLimitation = new GlobalSettings.ImportCiphersLimitationSettings();
        importCiphersLimitation.CiphersLimit = 40000;
        importCiphersLimitation.CollectionRelationshipsLimit = 80000;
        importCiphersLimitation.CollectionsLimit = 2000;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
                .With(c => c.ArchivedDate, (DateTime?)null)
                .CreateMany(1).ToArray())
            .With(y => y.Collections, fixture.Build<CollectionWithIdRequestModel>()
                .With(c => c.Id, orgIdGuid)
                .CreateMany(1).ToArray())
            .Create();

        // AccessImportExport permission setup
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Success());

        // BulkCollectionOperations.Create permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgIdGuid)
            .Returns(existingCollections.Select(c => new Collection { Id = orgIdGuid }).ToList());

        // Act
        await sutProvider.Sut.PostImportOrganization(orgId, request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received(1)
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_WithExistingCollectionsAndWithoutImportCiphersPermissions_ThrowsException(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = "AD89E6F8-4E84-4CFE-A978-256CC0DBF974";
        var orgIdGuid = Guid.Parse(orgId);
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
                .With(c => c.ArchivedDate, (DateTime?)null)
                .CreateMany(1).ToArray())
            .With(y => y.Collections, fixture.Build<CollectionWithIdRequestModel>()
                .With(c => c.Id, orgIdGuid)
                .CreateMany(1).ToArray())
            .Create();

        // AccessImportExport permission setup
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Failed());

        // BulkCollectionOperations.Create permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgIdGuid)
            .Returns(existingCollections.Select(c => new Collection { Id = orgIdGuid }).ToList());

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PostImportOrganization(orgId, request));

        // Assert
        Assert.IsType<Bit.Core.Exceptions.BadRequestException>(exception);
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_WithoutCreatePermissions_ThrowsException(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = "AD89E6F8-4E84-4CFE-A978-256CC0DBF974";
        var orgIdGuid = Guid.Parse(orgId);
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
                .With(c => c.ArchivedDate, (DateTime?)null)
                .CreateMany(1).ToArray())
            .With(y => y.Collections, fixture.Build<CollectionWithIdRequestModel>()
                .With(c => c.Id, orgIdGuid)
                .CreateMany(1).ToArray())
            .Create();

        // AccessImportExport permission setup
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Failed());

        // BulkCollectionOperations.Create permission setup
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgIdGuid)
            .Returns(existingCollections.Select(c => new Collection { Id = orgIdGuid }).ToList());

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PostImportOrganization(orgId, request));

        // Assert
        Assert.IsType<Bit.Core.Exceptions.BadRequestException>(exception);
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_CanCreateChildCollectionsWithCreateAndImportPermissionsAsync(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = Guid.NewGuid();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        // Create new collections
        var newCollections = fixture.Build<CollectionWithIdRequestModel>()
                .CreateMany(2).ToArray();

        // define existing collections
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        // import model includes new and existing collection
        var request = new ImportOrganizationCiphersRequestModel
        {
            Collections = newCollections.Concat(existingCollections).ToArray(),
            Ciphers = fixture.Build<CipherRequestModel>()
                .With(_ => _.OrganizationId, orgId.ToString())
                .With(_ => _.FolderId, Guid.NewGuid().ToString())
                .With(_ => _.ArchivedDate, (DateTime?)null)
                .CreateMany(2).ToArray(),
            CollectionRelationships = new List<KeyValuePair<int, int>>().ToArray(),
        };

        // AccessImportExport permission - false
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission - true
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Success());

        // BulkCollectionOperations.Create permission - true
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(existingCollections.Select(c =>
                new Collection { OrganizationId = orgId, Id = c.Id.GetValueOrDefault() })
                .ToList());

        // Act
        // User imports into collections and creates new collections
        // User has ImportCiphers and Create ciphers permission
        await sutProvider.Sut.PostImportOrganization(orgId.ToString(), request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received(1)
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_CannotCreateChildCollectionsWithoutCreatePermissionsAsync(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = Guid.NewGuid();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        // Create new collections
        var newCollections = fixture.Build<CollectionWithIdRequestModel>()
                .CreateMany(2).ToArray();

        // define existing collections
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        // import model includes new and existing collection
        var request = new ImportOrganizationCiphersRequestModel
        {
            Collections = newCollections.Concat(existingCollections).ToArray(),
            Ciphers = fixture.Build<CipherRequestModel>()
                .With(_ => _.OrganizationId, orgId.ToString())
                .With(_ => _.FolderId, Guid.NewGuid().ToString())
                .With(_ => _.ArchivedDate, (DateTime?)null)
                .CreateMany(2).ToArray(),
            CollectionRelationships = new List<KeyValuePair<int, int>>().ToArray(),
        };

        // AccessImportExport permission - false
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission - true
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Success());

        // BulkCollectionOperations.Create permission - FALSE
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(existingCollections.Select(c =>
                new Collection { OrganizationId = orgId, Id = c.Id.GetValueOrDefault() })
                .ToList());

        // Act
        // User imports into an existing collection and creates new collections
        // User has ImportCiphers permission only and doesn't have Create permission
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
        {
            await sutProvider.Sut.PostImportOrganization(orgId.ToString(), request);
        });

        // Assert
        Assert.IsType<BadRequestException>(exception);
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .DidNotReceive()
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ImportIntoNewCollectionWithCreatePermissionsOnlyAsync(
      SutProvider<ImportCiphersController> sutProvider,
      IFixture fixture,
      User user)
    {
        // Arrange
        var orgId = Guid.NewGuid();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        // Create new collections
        var newCollections = fixture.CreateMany<CollectionWithIdRequestModel>(1).ToArray();

        // Define existing collections
        var existingCollections = new List<CollectionWithIdRequestModel>();

        // Import model includes new and existing collection
        var request = new ImportOrganizationCiphersRequestModel
        {
            Collections = newCollections.Concat(existingCollections).ToArray(),
            Ciphers = fixture.Build<CipherRequestModel>()
                .With(_ => _.OrganizationId, orgId.ToString())
                .With(_ => _.FolderId, Guid.NewGuid().ToString())
                .With(_ => _.ArchivedDate, (DateTime?)null)
                .CreateMany(2).ToArray(),
            CollectionRelationships = new List<KeyValuePair<int, int>>().ToArray(),
        };

        // AccessImportExport permission - false
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission - FALSE
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Failed());

        // BulkCollectionOperations.Create permission - TRUE
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection>());

        // Act
        // User imports/creates a new collection - existing collections not affected
        // User has create permissions and doesn't need import permissions
        await sutProvider.Sut.PostImportOrganization(orgId.ToString(), request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received(1)
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ImportIntoExistingCollectionWithImportPermissionsOnlySuccessAsync(
      SutProvider<ImportCiphersController> sutProvider,
      IFixture fixture,
      User user)
    {
        // Arrange
        var orgId = Guid.NewGuid();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        // No new collections
        var newCollections = new List<CollectionWithIdRequestModel>();

        // Define existing collections
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(1).ToArray();

        // Import model includes new and existing collection
        var request = new ImportOrganizationCiphersRequestModel
        {
            Collections = newCollections.Concat(existingCollections).ToArray(),
            Ciphers = fixture.Build<CipherRequestModel>()
                .With(_ => _.OrganizationId, orgId.ToString())
                .With(_ => _.FolderId, Guid.NewGuid().ToString())
                .With(_ => _.ArchivedDate, (DateTime?)null)
                .CreateMany(2).ToArray(),
            CollectionRelationships = new List<KeyValuePair<int, int>>().ToArray(),
        };

        // AccessImportExport permission - false
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission - true
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Success());

        // BulkCollectionOperations.Create permission - FALSE
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(existingCollections.Select(c =>
                new Collection { OrganizationId = orgId, Id = c.Id.GetValueOrDefault() })
                .ToList());

        // Act
        // User import into existing collection
        // User has ImportCiphers permission only and doesn't need create permission
        await sutProvider.Sut.PostImportOrganization(orgId.ToString(), request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received(1)
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ImportWithNoCollectionsWithCreatePermissionsOnlySuccessAsync(
      SutProvider<ImportCiphersController> sutProvider,
      IFixture fixture,
      User user)
    {
        // Arrange
        var orgId = Guid.NewGuid();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        // Import model includes new and existing collection
        var request = new ImportOrganizationCiphersRequestModel
        {
            Collections = new List<CollectionWithIdRequestModel>().ToArray(),   // No collections
            Ciphers = fixture.Build<CipherRequestModel>()
                .With(_ => _.OrganizationId, orgId.ToString())
                .With(_ => _.FolderId, Guid.NewGuid().ToString())
                .With(_ => _.ArchivedDate, (DateTime?)null)
                .CreateMany(2).ToArray(),
            CollectionRelationships = new List<KeyValuePair<int, int>>().ToArray(),
        };

        // AccessImportExport permission - false
        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        // BulkCollectionOperations.ImportCiphers permission - false
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Failed());

        // BulkCollectionOperations.Create permission - TRUE
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection>());

        // Act
        // import ciphers only and no collections
        // User has Create permissions
        // expected to be successful
        await sutProvider.Sut.PostImportOrganization(orgId.ToString(), request);

        // Assert
        await sutProvider.GetDependency<IImportCiphersCommand>()
            .Received(1)
            .ImportIntoOrganizationalVaultAsync(
                Arg.Any<List<Collection>>(),
                Arg.Any<List<CipherDetails>>(),
                Arg.Any<IEnumerable<KeyValuePair<int, int>>>(),
                Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ThrowsException_WhenAnyCipherIsArchived(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user
    )
    {
        var orgId = Guid.NewGuid();

        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        sutProvider.GetDependency<GlobalSettings>()
            .ImportCiphersLimitation = _organizationCiphersLimitations;

        SetupUserService(sutProvider, user);

        var ciphers = fixture.Build<CipherRequestModel>()
                .With(_ => _.ArchivedDate, DateTime.UtcNow)
                .CreateMany(2).ToArray();

        var request = new ImportOrganizationCiphersRequestModel
        {
            Collections = new List<CollectionWithIdRequestModel>().ToArray(),
            Ciphers = ciphers,
            CollectionRelationships = new List<KeyValuePair<int, int>>().ToArray(),
        };

        sutProvider.GetDependency<ICurrentContext>()
            .AccessImportExport(Arg.Any<Guid>())
            .Returns(false);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.ImportCiphers)))
            .Returns(AuthorizationResult.Failed());

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Create)))
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<Collection>());

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
        {
            await sutProvider.Sut.PostImportOrganization(orgId.ToString(), request);
        });

        Assert.Equal("You cannot import archived items into an organization.", exception.Message);
    }

    private static void SetupUserService(SutProvider<ImportCiphersController> sutProvider, User user)
    {
        // This is a workaround for the NSubstitute issue with ambiguous arguments
        // when using Arg.Any<ClaimsPrincipal>() in the GetProperUserId method
        // It clears the previous calls to the userService and sets up a new call
        // with the same argument
        var userService = sutProvider.GetDependency<Bit.Core.Services.IUserService>();
        try
        {
            // in order to fix the Ambiguous Arguments error in NSubstitute
            // we need to clear the previous calls
            userService.ClearSubstitute();
            userService.ClearReceivedCalls();
            userService.GetProperUserId(Arg.Any<ClaimsPrincipal>());
        }
        catch { }

        userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(user.Id);
    }
}
