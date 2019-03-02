using System;
using System.Threading.Tasks;
using Xunit;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Bit.Core.Exceptions;

namespace Bit.Core.Test.Services
{
    public class CollectionServiceTest
    {
        private readonly IEventService _eventService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public CollectionServiceTest()
        {
            _eventService = Substitute.For<IEventService>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _collectionRepository = Substitute.For<ICollectionRepository>();
            _userRepository = Substitute.For<IUserRepository>();
            _mailService = Substitute.For<IMailService>();
        }

        [Fact]
        public async Task SaveAsync_CollectionNotFound()
        {
            var collectionService = new CollectionService(
                _eventService,
                _organizationRepository,
                _organizationUserRepository,
                _collectionRepository,
                _userRepository,
                _mailService);

            var id = Guid.NewGuid();

            var collection = new Models.Table.Collection
            {
                Id = id,
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => collectionService.SaveAsync(collection));

            Assert.Equal("Organization not found", ex.Message);
        }

        [Fact]
        public async Task SaveAsync_DefaultCollectionId_CreatesCollectionInTheRepository()
        {
            // prepare the organization
            var testOrganizationId = Guid.NewGuid();
            var testOrganization = new Models.Table.Organization
            {
                Id = testOrganizationId,
            };
            _organizationRepository.GetByIdAsync(testOrganizationId).Returns(testOrganization);

            var collectionService = new CollectionService(
                _eventService,
                _organizationRepository,
                _organizationUserRepository,
                _collectionRepository,
                _userRepository,
                _mailService);

            // execute
            var testCollection = new Models.Table.Collection
            {
                OrganizationId = testOrganizationId,
            };
            await collectionService.SaveAsync(testCollection);

            // verify
            await _collectionRepository.Received().CreateAsync(testCollection);
        }

        [Fact]
        public async Task SaveAsync_RespectsMaxNumberOfCollectionsPerOrganization()
        {
            // prepare the organization
            var testOrganizationId = Guid.NewGuid();
            var testOrganization = new Models.Table.Organization
            {
                Id = testOrganizationId,
                MaxCollections = 2,
            };
            _organizationRepository.GetByIdAsync(testOrganizationId).Returns(testOrganization);
            _collectionRepository.GetCountByOrganizationIdAsync(testOrganizationId).Returns(2);

            // execute
            var collectionService = new CollectionService(
                _eventService,
                _organizationRepository,
                _organizationUserRepository,
                _collectionRepository,
                _userRepository,
                _mailService);

            var testCollection = new Models.Table.Collection { OrganizationId = testOrganizationId };

            // verify & expect exception to be thrown
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => collectionService.SaveAsync(testCollection));

            Assert.Equal("You have reached the maximum number of collections (2) for this organization.",
                ex.Message);
        }

        [Fact]
        public async Task DeleteUserAsync_DeletesValidUserWhoBelongsToCollection()
        {
            // prepare the organization
            var testOrganizationId = Guid.NewGuid();
            var testOrganization = new Models.Table.Organization
            {
                Id = testOrganizationId,
            };
            var testUserId = Guid.NewGuid();
            var organizationUser = new Models.Table.OrganizationUser
            {
                Id = testUserId,
                OrganizationId = testOrganizationId,
            };
            _organizationUserRepository.GetByIdAsync(testUserId).Returns(organizationUser);

            // execute
            var collectionService = new CollectionService(
                _eventService,
                _organizationRepository,
                _organizationUserRepository,
                _collectionRepository,
                _userRepository,
                _mailService);

            var testCollection = new Models.Table.Collection { OrganizationId = testOrganizationId };
            await collectionService.DeleteUserAsync(testCollection, organizationUser.Id);

            // verify
            await _collectionRepository.Received().DeleteUserAsync(testCollection.Id, organizationUser.Id);
        }

        [Fact]
        public async Task DeleteUserAsync_ThrowsIfUserIsInvalid()
        {
            // prepare the organization
            var testOrganizationId = Guid.NewGuid();
            var testOrganization = new Models.Table.Organization
            {
                Id = testOrganizationId,
            };
            var testUserId = Guid.NewGuid();
            var nonOrganizationUser = new Models.Table.OrganizationUser
            {
                Id = testUserId,
                OrganizationId = Guid.NewGuid(),
            };
            _organizationUserRepository.GetByIdAsync(testUserId).Returns(nonOrganizationUser);

            // execute
            var collectionService = new CollectionService(
                _eventService,
                _organizationRepository,
                _organizationUserRepository,
                _collectionRepository,
                _userRepository,
                _mailService);

            var testCollection = new Models.Table.Collection { OrganizationId = testOrganizationId };

            // verify
            // invalid user
            await Assert.ThrowsAsync<NotFoundException>(() =>
                collectionService.DeleteUserAsync(testCollection, Guid.NewGuid()));
            // user from other organization
            await Assert.ThrowsAsync<NotFoundException>(() =>
                collectionService.DeleteUserAsync(testCollection, testUserId));
        }
    }
}
