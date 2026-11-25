using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class SendAccessQueryTests
{
    private readonly ISendRepository _sendRepository;
    private readonly SendAccessQuery _sendAccessQuery;

    public SendAccessQueryTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _sendAccessQuery = new SendAccessQuery(_sendRepository);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SendAccessQuery(null));

        Assert.Equal("sendRepository", exception.ParamName);
    }

    [Fact]
    public async Task Get_WhenSendExists_ReturnsSend()
    {
        var sendId = Guid.NewGuid();
        var send = new Send { Id = sendId };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        var result = await _sendAccessQuery.Get(sendId);

        Assert.Same(send, result);
    }

    [Fact]
    public async Task Get_WhenSendDoesNotExist_ReturnsNull()
    {
        var sendId = Guid.NewGuid();
        _sendRepository.GetByIdAsync(sendId).Returns((Send?)null);

        var result = await _sendAccessQuery.Get(sendId);

        Assert.Null(result);
    }
}
