using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Subscriptions;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bit.Core.Test.Billing.Commands;

public class BaseBillingCommandTests
{
    private class TestCommand() : BaseBillingCommand<TestCommand>(NullLogger<TestCommand>.Instance)
    {
        public Task<BillingCommandResult<int>> Run(Exception toThrow) =>
            HandleAsync<int>(() => throw toThrow);
    }

    [Fact]
    public async Task HandleAsync_InvoicePreviewExpiredException_Propagates() =>
        await Assert.ThrowsAsync<InvoicePreviewExpiredException>(() =>
            new TestCommand().Run(new InvoicePreviewExpiredException()));

    [Fact]
    public async Task HandleAsync_OtherBadRequestException_StillReturnsUnhandled()
    {
        var result = await new TestCommand().Run(new BadRequestException("x"));

        Assert.True(result.IsT3);
    }
}
