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
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Api.Test.Tools.Controllers;

[ControllerCustomize(typeof(ImportCiphersController))]
[SutProviderCustomize]
public class ImportCiphersControllerTests
{

    /*************************
     * PostImport - Individual
     *************************/
    [Theory, BitAutoData]
    public async Task PostImportIndividual_ImportCiphersRequestModel_BadRequestException(SutProvider<ImportCiphersController> sutProvider, IFixture fixture)
    {
        // Arrange
        sutProvider.GetDependency<Core.Settings.GlobalSettings>()
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

    /****************************
     * PostImport - Organization
     ****************************/

    [Theory, BitAutoData]
    public async Task PostImportOrganization_ImportOrganizationCiphersRequestModel_BadRequestException(SutProvider<ImportCiphersController> sutProvider, IFixture fixture)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<Core.Settings.GlobalSettings>();
        globalSettings.SelfHosted = false;
        globalSettings.ImportCiphersLimitation = new GlobalSettings.ImportCiphersLimitationSettings()
        { // limits are set in appsettings.json, making values small for test to run faster.
            CiphersLimit = 200,
            CollectionsLimit = 400,
            CollectionRelationshipsLimit = 20
        };

        var ciphers = fixture.CreateMany<CipherRequestModel>(201).ToArray();
        var model = new ImportOrganizationCiphersRequestModel
        {
            Collections = null,
            Ciphers = ciphers,
            CollectionRelationships = null
        };

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostImport(Arg.Any<string>(), model));

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

        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
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
        await sutProvider.Sut.PostImport(orgId, request);

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

        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
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
        await sutProvider.Sut.PostImport(orgId, request);

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
    public async Task PostImportOrganization_WithExistingCollectionsAndWithoutImportCiphersPermissions_NotFoundException(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = "AD89E6F8-4E84-4CFE-A978-256CC0DBF974";
        var orgIdGuid = Guid.Parse(orgId);
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
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
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(orgIdGuid)
            .Returns(existingCollections.Select(c => new Collection { Id = orgIdGuid }).ToList());

        // Act
        var exception = await Assert.ThrowsAsync<Bit.Core.Exceptions.NotFoundException>(() =>
            sutProvider.Sut.PostImport(orgId, request));

        // Assert
        Assert.IsType<Bit.Core.Exceptions.NotFoundException>(exception);
    }

    [Theory, BitAutoData]
    public async Task PostImportOrganization_WithoutCreatePermissions_NotFoundException(
        SutProvider<ImportCiphersController> sutProvider,
        IFixture fixture,
        User user)
    {
        // Arrange
        var orgId = "AD89E6F8-4E84-4CFE-A978-256CC0DBF974";
        var orgIdGuid = Guid.Parse(orgId);
        var existingCollections = fixture.CreateMany<CollectionWithIdRequestModel>(2).ToArray();

        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;

        sutProvider.GetDependency<Bit.Core.Services.IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        var request = fixture.Build<ImportOrganizationCiphersRequestModel>()
            .With(x => x.Ciphers, fixture.Build<CipherRequestModel>()
                .With(c => c.OrganizationId, Guid.NewGuid().ToString())
                .With(c => c.FolderId, Guid.NewGuid().ToString())
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
            .Returns(AuthorizationResult.Success());

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
        var exception = await Assert.ThrowsAsync<Bit.Core.Exceptions.NotFoundException>(() =>
            sutProvider.Sut.PostImport(orgId, request));

        // Assert
        Assert.IsType<Bit.Core.Exceptions.NotFoundException>(exception);
    }
}
