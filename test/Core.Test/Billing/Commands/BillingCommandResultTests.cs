using Bit.Core.Billing.Commands;
using Bit.Core.Exceptions;
using Xunit;

namespace Bit.Core.Test.Billing.Commands;

public class BillingCommandResultTests
{
    [Fact]
    public void GetValueOrThrowHttpException_Success_ReturnsTheValue()
    {
        BillingCommandResult<int> result = 7;

        Assert.Equal(7, result.GetValueOrThrowHttpException());
    }

    [Fact]
    public void GetValueOrThrowHttpException_BadRequest_ThrowsBadRequestWithItsMessage()
    {
        BillingCommandResult<int> result = new BadRequest("Your card was declined.");

        var exception = Assert.Throws<BadRequestException>(() => result.GetValueOrThrowHttpException());

        Assert.Equal("Your card was declined.", exception.Message);
    }

    [Fact]
    public void GetValueOrThrowHttpException_Conflict_ThrowsConflictWithItsMessage()
    {
        BillingCommandResult<int> result = new Conflict("Try again later.");

        var exception = Assert.Throws<ConflictException>(() => result.GetValueOrThrowHttpException());

        Assert.Equal("Try again later.", exception.Message);
    }

    [Fact]
    public void GetValueOrThrowHttpException_UnhandledWithException_RethrowsTheUnderlyingException()
    {
        var underlying = new InvalidOperationException("Stripe is down.");
        BillingCommandResult<int> result = new Unhandled(underlying);

        var exception = Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrowHttpException());

        Assert.Same(underlying, exception);
    }

    [Fact]
    public void GetValueOrThrowHttpException_UnhandledWithoutException_ThrowsInvalidOperationWithTheResponse()
    {
        BillingCommandResult<int> result = new Unhandled(Response: "Something broke.");

        var exception = Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrowHttpException());

        Assert.Equal("Something broke.", exception.Message);
    }
}
