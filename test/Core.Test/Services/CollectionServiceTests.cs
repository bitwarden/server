using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class CollectionServiceTest
    {
        [Theory, CollectionDefaultIdAutoData]
        public async Task SaveAsync_DefaultId_CreatesCollectionInTheRepository(Collection collection, Organization organization, SutProvider<CollectionService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(collection);

            await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection);
            await sutProvider.GetDependency<IEventService>().Received()
                .LogCollectionEventAsync(collection, EventType.Collection_Created);
            Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CollectionDefaultIdAutoData]
        public async Task SaveAsync_DefaultIdWithGroups_CreateCollectionWithGroupsInRepository(Collection collection,
            IEnumerable<SelectionReadOnly> groups, Organization organization, OrganizationUser organizationUser,
            SutProvider<CollectionService> sutProvider)
        {
            organization.UseGroups = true;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(collection, groups);

            await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection, groups);
            await sutProvider.GetDependency<IEventService>().Received()
                .LogCollectionEventAsync(collection, EventType.Collection_Created);
            Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CollectionNonDefaultIdAutoData]
        public async Task SaveAsync_NonDefaultId_ReplacesCollectionInRepository(Collection collection, Organization organization, SutProvider<CollectionService> sutProvider)
        {
            var creationDate = collection.CreationDate;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(collection);

            await sutProvider.GetDependency<ICollectionRepository>().Received().ReplaceAsync(collection);
            await sutProvider.GetDependency<IEventService>().Received()
                .LogCollectionEventAsync(collection, EventType.Collection_Updated);
            Assert.Equal(collection.CreationDate, creationDate);
            Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CollectionDefaultIdAutoData]
        public async Task SaveAsync_OrganizationNotUseGroup_CreateCollectionWithoutGroupsInRepository(Collection collection, IEnumerable<SelectionReadOnly> groups,
            Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(collection, groups);

            await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection);
            await sutProvider.GetDependency<IEventService>().Received()
                .LogCollectionEventAsync(collection, EventType.Collection_Created);
            Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CollectionDefaultIdAutoData]
        public async Task SaveAsync_DefaultIdWithUserId_UpdateUserInCollectionRepository(Collection collection,
            Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
        {
            organizationUser.Status = OrganizationUserStatusType.Confirmed;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(organization.Id, organizationUser.Id)
                .Returns(organizationUser);
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(collection, null, organizationUser.Id);

            await sutProvider.GetDependency<ICollectionRepository>().Received().CreateAsync(collection);
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received()
                .GetByOrganizationAsync(organization.Id, organizationUser.Id);
            await sutProvider.GetDependency<ICollectionRepository>().Received().UpdateUsersAsync(collection.Id, Arg.Any<List<SelectionReadOnly>>());
            await sutProvider.GetDependency<IEventService>().Received()
                .LogCollectionEventAsync(collection, EventType.Collection_Created);
            Assert.True(collection.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(collection.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_NonExistingOrganizationId_ThrowsBadRequest(Collection collection, SutProvider<CollectionService> sutProvider)
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveAsync(collection));
            Assert.Contains("Organization not found", ex.Message);
            await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
            await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCollectionEventAsync(default, default);
        }

        [Theory, CollectionDefaultIdAutoData]
        public async Task SaveAsync_ExceedsOrganizationMaxCollections_ThrowsBadRequest(Collection collection, Collection collection1, Collection collection2, Organization organization, SutProvider<CollectionService> sutProvider)
        {
            organization.MaxCollections = 2;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<ICollectionRepository>().GetCountByOrganizationIdAsync(organization.Id)
                .Returns(2);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SaveAsync(collection));
            Assert.Equal("You have reached the maximum number of collections (2) for this organization.", ex.Message);
            await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
            await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogCollectionEventAsync(default, default);
        }

        [Theory, CollectionNonDefaultIdAutoData]
        public async Task DeleteUserAsync_DeletesValidUserWhoBelongsToCollection(Collection collection,
            Organization organization, OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
        {
            collection.OrganizationId = organization.Id;
            organizationUser.OrganizationId = organization.Id;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
                .Returns(organizationUser);

            await sutProvider.Sut.DeleteUserAsync(collection, organizationUser.Id);

            await sutProvider.GetDependency<ICollectionRepository>().Received()
                .DeleteUserAsync(collection.Id, organizationUser.Id);
            await sutProvider.GetDependency<IEventService>().Received().LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);
        }

        [Theory, CollectionNonDefaultIdAutoData]
        public async Task DeleteUserAsync_InvalidUser_ThrowsNotFound(Collection collection, Organization organization,
            OrganizationUser organizationUser, SutProvider<CollectionService> sutProvider)
        {
            collection.OrganizationId = organization.Id;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
                .Returns(organizationUser);

            // user not in organization
            await Assert.ThrowsAsync<NotFoundException>(() =>
                sutProvider.Sut.DeleteUserAsync(collection, organizationUser.Id));
            // invalid user
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteUserAsync(collection, Guid.NewGuid()));
            await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
            await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
                .LogOrganizationUserEventAsync(default, default);
        }
    }
}
