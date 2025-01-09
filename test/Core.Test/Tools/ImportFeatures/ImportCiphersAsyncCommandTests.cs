﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.ImportFeatures;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
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
    public async Task ImportCiphersAsync_IntoOrganization_Success(
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

        await sutProvider.Sut.ImportCiphersAsync(collections, ciphers, collectionRelationships, importingUserId);

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
        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(
            Arg.Is<ReferenceEvent>(e => e.Type == ReferenceEventType.VaultImported));
    }

    [Theory, BitAutoData]
    public async Task ImportCiphersAsync_ThrowsBadRequestException_WhenAnyPoliciesApplicableToUser(
        List<Folder> folders,
        List<CipherDetails> ciphers,
        SutProvider<ImportCiphersCommand> sutProvider)
    {
        var userId = Guid.NewGuid();
        folders.ForEach(f => f.UserId = userId);
        ciphers.ForEach(c => c.UserId = userId);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.PersonalOwnership)
            .Returns(true);

        var folderRelationships = new List<KeyValuePair<int, int>>();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ImportCiphersAsync(folders, ciphers, folderRelationships));

        Assert.Equal("You cannot import items into your personal vault because you are a member of an organization which forbids it.", exception.Message);
    }
}
