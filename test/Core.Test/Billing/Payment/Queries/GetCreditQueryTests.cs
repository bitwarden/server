using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Queries;

public class GetCreditQueryTests
{
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly GetCreditQuery _query;

    public GetCreditQueryTests()
    {
        _query = new GetCreditQuery(_subscriberService);
    }

    [Fact]
    public async Task Run_NoCustomer_ReturnsNull()
    {
        _subscriberService.GetCustomer(Arg.Any<ISubscriber>()).ReturnsNull();

        var credit = await _query.Run(Substitute.For<ISubscriber>());

        Assert.Null(credit);
    }

    [Fact]
    public async Task Run_ReturnsCredit()
    {
        _subscriberService.GetCustomer(Arg.Any<ISubscriber>()).Returns(new Customer { Balance = -1000 });

        var credit = await _query.Run(Substitute.For<ISubscriber>());

        Assert.NotNull(credit);
        Assert.Equal(10M, credit);
    }
}
