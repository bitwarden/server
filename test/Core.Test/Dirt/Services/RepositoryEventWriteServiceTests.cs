using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class RepositoryEventWriteServiceTests
{
    private readonly RepositoryEventWriteService _sut;

    private readonly IEventRepository _eventRepository;

    public RepositoryEventWriteServiceTests()
    {
        _eventRepository = Substitute.For<IEventRepository>();

        _sut = new RepositoryEventWriteService(_eventRepository);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
