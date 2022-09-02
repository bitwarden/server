using Amazon.SQS;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class AmazonSqsBlockIpServiceTests : IDisposable
{
    private readonly AmazonSqsBlockIpService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly IAmazonSQS _amazonSqs;

    public AmazonSqsBlockIpServiceTests()
    {
        _globalSettings = new GlobalSettings
        {
            Amazon =
            {
                AccessKeyId = "AccessKeyId-AmazonSesMailDeliveryServiceTests",
                AccessKeySecret = "AccessKeySecret-AmazonSesMailDeliveryServiceTests",
                Region = "Region-AmazonSesMailDeliveryServiceTests"
            }
        };

        _amazonSqs = Substitute.For<IAmazonSQS>();

        _sut = new AmazonSqsBlockIpService(_globalSettings, _amazonSqs);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public async Task BlockIpAsync_UnblockCalled_WhenNotPermanent()
    {
        const string expectedIp = "ip";

        await _sut.BlockIpAsync(expectedIp, false);

        await _amazonSqs.Received(2).SendMessageAsync(
            Arg.Any<string>(),
            Arg.Is(expectedIp));
    }

    [Fact]
    public async Task BlockIpAsync_UnblockNotCalled_WhenPermanent()
    {
        const string expectedIp = "ip";

        await _sut.BlockIpAsync(expectedIp, true);

        await _amazonSqs.Received(1).SendMessageAsync(
            Arg.Any<string>(),
            Arg.Is(expectedIp));
    }

    [Fact]
    public async Task BlockIpAsync_NotBlocked_WhenAlreadyBlockedRecently()
    {
        const string expectedIp = "ip";

        await _sut.BlockIpAsync(expectedIp, true);

        // The second call should hit the already blocked guard clause
        await _sut.BlockIpAsync(expectedIp, true);

        await _amazonSqs.Received(1).SendMessageAsync(
            Arg.Any<string>(),
            Arg.Is(expectedIp));
    }
}
