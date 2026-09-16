#nullable enable

using Bit.Core.Dirt.Services.Implementations;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

public class RabbitMqServiceTests
{
    [Fact]
    public void BuildDeadLetterQueueArguments_PositiveTimeToLive_SetsMessageTtlInMilliseconds()
    {
        var arguments = RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.FromDays(2));

        Assert.NotNull(arguments);
        Assert.Equal(172800000, arguments["x-message-ttl"]);
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_ZeroTimeToLive_ReturnsNoArguments()
    {
        Assert.Null(RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.Zero));
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_NegativeTimeToLive_ReturnsNoArguments()
    {
        Assert.Null(RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void BuildDeadLetterQueueArguments_TimeToLiveBeyondIntRange_CapsAtMaxValue()
    {
        var arguments = RabbitMqService.BuildDeadLetterQueueArguments(TimeSpan.FromDays(365));

        Assert.NotNull(arguments);
        Assert.Equal(int.MaxValue, arguments["x-message-ttl"]);
    }
}
