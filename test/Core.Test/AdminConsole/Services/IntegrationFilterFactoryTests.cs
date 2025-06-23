using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

public class IntegrationFilterFactoryTests
{
    [Theory, BitAutoData]
    public void BuildEqualityFilter_ReturnsCorrectMatch(EventMessage message)
    {
        var different = Guid.NewGuid();
        var expected = Guid.NewGuid();
        message.UserId = expected;

        var filter = IntegrationFilterFactory.BuildEqualityFilter<Guid?>("UserId");

        Assert.True(filter(message, expected));
        Assert.False(filter(message, different));
    }

    [Theory, BitAutoData]
    public void BuildEqualityFilter_UserIdIsNull_ReturnsFalse(EventMessage message)
    {
        message.UserId = null;

        var filter = IntegrationFilterFactory.BuildEqualityFilter<Guid?>("UserId");

        Assert.False(filter(message, Guid.NewGuid()));
    }

    [Theory, BitAutoData]
    public void BuildInFilter_ReturnsCorrectMatch(EventMessage message)
    {
        var match = Guid.NewGuid();
        message.UserId = match;
        var inList = new List<Guid?> { Guid.NewGuid(), match, Guid.NewGuid() };
        var outList = new List<Guid?> { Guid.NewGuid(), Guid.NewGuid() };

        var filter = IntegrationFilterFactory.BuildInFilter<Guid?>("UserId");

        Assert.True(filter(message, inList));
        Assert.False(filter(message, outList));
    }
}
