using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Services;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class ReceiveAuthorizationServiceTests
{
    private readonly ReceiveAuthorizationService _sut;

    public ReceiveAuthorizationServiceTests()
    {
        _sut = new ReceiveAuthorizationService();
    }

    private static Receive CreateReceive(DateTime? expirationDate = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Data = "{}",
        UserKeyWrappedSharedContentEncryptionKey = "2.scek|iv|ct",
        UserKeyWrappedPrivateKey = "2.privkey|iv|ct",
        ScekWrappedPublicKey = "2.pubkey|iv|ct",
        Secret = "secret",
        ExpirationDate = expirationDate,
    };

    [Fact]
    public void ReceiveCanBeAccessed_NoExpirationDate_ReturnsTrue()
    {
        var receive = CreateReceive(expirationDate: null);

        var result = _sut.ReceiveCanBeAccessed(receive);

        Assert.True(result);
    }

    [Fact]
    public void ReceiveCanBeAccessed_FutureExpirationDate_ReturnsTrue()
    {
        var receive = CreateReceive(expirationDate: DateTime.UtcNow.AddYears(1));

        var result = _sut.ReceiveCanBeAccessed(receive);

        Assert.True(result);
    }

    [Fact]
    public void ReceiveCanBeAccessed_PastExpirationDate_ReturnsFalse()
    {
        var receive = CreateReceive(expirationDate: DateTime.UtcNow.AddDays(-1));

        var result = _sut.ReceiveCanBeAccessed(receive);

        Assert.False(result);
    }

    [Fact]
    public void Access_DelegatesToReceiveCanBeAccessed()
    {
        var validReceive = CreateReceive(expirationDate: DateTime.UtcNow.AddYears(1));
        var expiredReceive = CreateReceive(expirationDate: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(_sut.ReceiveCanBeAccessed(validReceive), _sut.Access(validReceive));
        Assert.Equal(_sut.ReceiveCanBeAccessed(expiredReceive), _sut.Access(expiredReceive));
    }
}
