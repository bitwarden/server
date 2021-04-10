using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class GroupServiceTests
    {
        private readonly GroupService _sut;

        private readonly IEventService _eventService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IGroupRepository _groupRepository;

        public GroupServiceTests()
        {
            _eventService = Substitute.For<IEventService>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _groupRepository = Substitute.For<IGroupRepository>();

            _sut = new GroupService(
                _eventService,
                _organizationRepository,
                _organizationUserRepository,
                _groupRepository
            );
        }

        [Fact]
        public async Task SaveAsync_DefaultGroupId_CreatesGroupInRepository()
        {
            var organization = new Models.Table.Organization
            {
                Id = Guid.NewGuid(),
                UseGroups = true,
            };
            _organizationRepository
                .GetByIdAsync(organization.Id)
                .Returns(organization);
            var group = new Models.Table.Group
            {
                OrganizationId = organization.Id,
            };

            await _sut.SaveAsync(group);

            await _groupRepository.Received().CreateAsync(group);
        }

        [Fact]
        public async Task SaveAsync_NonExistingOrganizationId_ThrowsBadRequest()
        {
            var group = new Models.Table.Group
            {
                Id = Guid.NewGuid(),
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.SaveAsync(group));
        }

        [Fact]
        public async Task SaveAsync_OrganizationDoesNotUseGroups_ThrowsBadRequest()
        {
            var organization = new Models.Table.Organization
            {
                Id = Guid.NewGuid(),
            };
            _organizationRepository
                .GetByIdAsync(organization.Id)
                .Returns(organization);
            var group = new Models.Table.Group
            {
                OrganizationId = organization.Id,
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.SaveAsync(group));
        }

        [Fact]
        public async Task DeleteUserAsync_ValidData_DeletesUserInGroupRepository()
        {
            // organization
            var organization = new Models.Table.Organization
            {
                Id = Guid.NewGuid(),
                UseGroups = true,
            };
            _organizationRepository
                .GetByIdAsync(organization.Id)
                .Returns(organization);
            // user
            var organizationUser = new Models.Table.OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
            };
            _organizationUserRepository.GetByIdAsync(organizationUser.Id)
                .Returns(organizationUser);
            // group
            var group = new Models.Table.Group
            {
                OrganizationId = organization.Id,
            };

            await _sut.DeleteUserAsync(group, organizationUser.Id);

            await _groupRepository.Received().DeleteUserAsync(group.Id, organizationUser.Id);
        }

        [Fact]
        public async Task DeleteUserAsync_InvalidUser_ThrowsNotFound()
        {
            // user
            var nonOrganizationUser = new Models.Table.OrganizationUser
            {
                Id = Guid.NewGuid(),
            };
            _organizationUserRepository.GetByIdAsync(nonOrganizationUser.Id)
                .Returns(nonOrganizationUser);
            // organization
            var organizationId = Guid.NewGuid();
            var organization = new Models.Table.Organization
            {
                Id = organizationId,
            };
            _organizationRepository
                .GetByIdAsync(organization.Id)
                .Returns(organization);
            // group
            var group = new Models.Table.Group
            {
                OrganizationId = organization.Id,
            };

            // user not in organization
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteUserAsync(group, nonOrganizationUser.Id));
            // invalid user
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteUserAsync(group, Guid.NewGuid()));
        }
    }
}
