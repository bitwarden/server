using System;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class EventServiceTests
    {
        private readonly EventService _sut;

        private readonly IEventWriteService _eventWriteService;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public EventServiceTests()
        {
            _eventWriteService = Substitute.For<IEventWriteService>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _applicationCacheService = Substitute.For<IApplicationCacheService>();
            _currentContext = new CurrentContext(null);
            _globalSettings = new GlobalSettings();

            _sut = new EventService(
                _eventWriteService,
                _organizationUserRepository,
                _applicationCacheService,
                _currentContext,
                _globalSettings
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
