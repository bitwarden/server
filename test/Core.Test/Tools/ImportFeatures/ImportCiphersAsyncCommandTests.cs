using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Tools.ImportFeatures;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.ImportFeatures;

[UserCipherCustomize]
[SutProviderCustomize]
public class ImportCiphersAsyncCommandTests
{
    [Theory, BitAutoData]
    public async Task ImportIntoIndividualVaultAsync_Success(
        Guid importingUserId,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(importingUserId, PolicyType.OrganizationDataOwnership)
            .Returns(false);

        sutProvider.GetDependency<IFolderRepository>()
            .GetManyByUserIdAsync(importingUserId)
            .Returns(new List<Folder>());

        var folders = new List<Folder> { new Folder { UserId = importingUserId } };

        var folderRelationships = new List<KeyValuePair<int, int>>();

        // Act
        await sutProvider.Sut.ImportIntoIndividualVaultAsync(folders, ciphers, folderRelationships, importingUserId);

        // Assert
        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .CreateAsync(importingUserId, ciphers, Arg.Any<List<Folder>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncVaultAsync(importingUserId);
    }

    [Theory, BitAutoData]
    public async Task ImportIntoIndividualVaultAsync_WithPolicyRequirementsEnabled_WithOrganizationDataOwnershipPolicyDisabled_Success(
        Guid importingUserId,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(importingUserId)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(
                OrganizationDataOwnershipState.Disabled,
                []));

        sutProvider.GetDependency<IFolderRepository>()
            .GetManyByUserIdAsync(importingUserId)
            .Returns(new List<Folder>());

        var folders = new List<Folder> { new Folder { UserId = importingUserId } };

        var folderRelationships = new List<KeyValuePair<int, int>>();

        await sutProvider.Sut.ImportIntoIndividualVaultAsync(folders, ciphers, folderRelationships, importingUserId);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .CreateAsync(importingUserId, ciphers, Arg.Any<List<Folder>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncVaultAsync(importingUserId);
    }

    [Theory, BitAutoData]
    public async Task ImportIntoIndividualVaultAsync_ThrowsBadRequestException(
        List<Folder> folders,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        var userId = Guid.NewGuid();
        folders.ForEach(f => f.UserId = userId);
        ciphers.ForEach(c => c.UserId = userId);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.OrganizationDataOwnership)
            .Returns(true);

        var folderRelationships = new List<KeyValuePair<int, int>>();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ImportIntoIndividualVaultAsync(folders, ciphers, folderRelationships, userId));

        Assert.Equal("You cannot import items into your personal vault because you are a member of an organization which forbids it.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ImportIntoIndividualVaultAsync_WithPolicyRequirementsEnabled_WithOrganizationDataOwnershipPolicyEnabled_ThrowsBadRequestException(
        List<Folder> folders,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        var userId = Guid.NewGuid();
        folders.ForEach(f => f.UserId = userId);
        ciphers.ForEach(c => c.UserId = userId);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(
                OrganizationDataOwnershipState.Enabled,
                [new PolicyDetails()]));

        var folderRelationships = new List<KeyValuePair<int, int>>();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ImportIntoIndividualVaultAsync(folders, ciphers, folderRelationships, userId));

        Assert.Equal("You cannot import items into your personal vault because you are a member of an organization which forbids it.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ImportIntoOrganizationalVaultAsync_Success(
        Organization organization,
        Guid importingUserId,
        OrganizationUser importingOrganizationUser,
        List<Collection> collections,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        organization.MaxCollections = null;
        importingOrganizationUser.OrganizationId = organization.Id;

        foreach (var collection in collections)
        {
            collection.OrganizationId = organization.Id;
        }

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        KeyValuePair<int, int>[] collectionRelationships = {
            new(0, 0),
            new(1, 1),
            new(2, 2)
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, importingUserId)
            .Returns(importingOrganizationUser);

        // Set up a collection that already exists in the organization
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Collection> { collections[0] });

        await sutProvider.Sut.ImportIntoOrganizationalVaultAsync(collections, ciphers, collectionRelationships, importingUserId);

        await sutProvider.GetDependency<ICipherRepository>().Received(1).CreateAsync(
            ciphers,
            Arg.Is<IEnumerable<Collection>>(cols => cols.Count() == collections.Count - 1 &&
                        !cols.Any(c => c.Id == collections[0].Id) && // Check that the collection that already existed in the organization was not added
                        cols.All(c => collections.Any(x => c.Name == x.Name))),
            Arg.Is<IEnumerable<CollectionCipher>>(c => c.Count() == ciphers.Count),
            Arg.Is<IEnumerable<CollectionUser>>(cus =>
                cus.Count() == collections.Count - 1 &&
                !cus.Any(cu => cu.CollectionId == collections[0].Id) && // Check that access was not added for the collection that already existed in the organization
                cus.All(cu => cu.OrganizationUserId == importingOrganizationUser.Id && cu.Manage == true)));
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncVaultAsync(importingUserId);
    }

    [Theory, BitAutoData]
    public async Task ImportIntoOrganizationalVaultAsync_ThrowsBadRequestException(
        Organization organization,
        Guid importingUserId,
        OrganizationUser importingOrganizationUser,
        List<Collection> collections,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        organization.MaxCollections = 1;
        importingOrganizationUser.OrganizationId = organization.Id;

        foreach (var collection in collections)
        {
            collection.OrganizationId = organization.Id;
        }

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        KeyValuePair<int, int>[] collectionRelationships = {
            new(0, 0),
            new(1, 1),
            new(2, 2)
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, importingUserId)
            .Returns(importingOrganizationUser);

        // Set up a collection that already exists in the organization
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Collection> { collections[0] });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ImportIntoOrganizationalVaultAsync(collections, ciphers, collectionRelationships, importingUserId));

        Assert.Equal("This organization can only have a maximum of " +
        $"{organization.MaxCollections} collections.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ImportIntoOrganizationalVaultAsync_WithNullImportingOrgUser_SkipsCollectionUserCreation(
        Organization organization,
        Guid importingUserId,
        List<Collection> collections,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        organization.MaxCollections = null;

        foreach (var collection in collections)
        {
            collection.OrganizationId = organization.Id;
        }

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        KeyValuePair<int, int>[] collectionRelationships = {
            new(0, 0),
            new(1, 1),
            new(2, 2)
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Simulate provider-created org with no members - importing user is NOT an org member
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, importingUserId)
            .Returns((OrganizationUser)null);

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(new List<Collection>());

        await sutProvider.Sut.ImportIntoOrganizationalVaultAsync(collections, ciphers, collectionRelationships, importingUserId);

        // Verify ciphers were created but no CollectionUser entries were created (because the organization user (importingUserId) is null)
        await sutProvider.GetDependency<ICipherRepository>().Received(1).CreateAsync(
            ciphers,
            Arg.Is<IEnumerable<Collection>>(cols => cols.Count() == collections.Count),
            Arg.Is<IEnumerable<CollectionCipher>>(cc => cc.Count() == ciphers.Count),
            Arg.Is<IEnumerable<CollectionUser>>(cus => !cus.Any()));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncVaultAsync(importingUserId);
    }
}
