using System;
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

        // Remove this test when we add actual tests. It only proves that
        // we've properly constructed the system under test.
        [Fact]
        public void ServiceExists()
        {
            Assert.NotNull(_sut);
        }
    }
}
