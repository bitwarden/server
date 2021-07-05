using System;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class InMemoryApplicationCacheServiceTests
    {
        private readonly InMemoryApplicationCacheService _sut;

        private readonly IOrganizationRepository _organizationRepository;

        public InMemoryApplicationCacheServiceTests()
        {
            _organizationRepository = Substitute.For<IOrganizationRepository>();

            _sut = new InMemoryApplicationCacheService(_organizationRepository);
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
