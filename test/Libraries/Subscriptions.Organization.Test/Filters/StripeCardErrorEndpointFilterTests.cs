using Bit.Core.Billing.Constants;
using Bit.Core.Exceptions;
using Bit.Subscriptions.Organization.Filters;
using Microsoft.AspNetCore.Http;
using Stripe;
using Xunit;

namespace Bit.Subscriptions.Organization.Test.Filters;

public class StripeCardErrorEndpointFilterTests
{
    private readonly StripeCardErrorEndpointFilter _sut = new();
    private readonly EndpointFilterInvocationContext _context =
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());

    [Fact]
    public async Task InvokeAsync_NoException_ReturnsResultFromNext()
    {
        var expected = new object();

        var result = await _sut.InvokeAsync(_context, _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_CardError_ThrowsBadRequestWithStripeMessage()
    {
        var cardError = new StripeException
        {
            StripeError = new StripeError { Type = StripeConstants.ErrorTypes.CardError, Message = "Your card was declined." }
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await _sut.InvokeAsync(_context, _ => throw cardError));

        Assert.Equal("Your card was declined.", exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_NonCardStripeError_PropagatesStripeException()
    {
        var apiError = new StripeException { StripeError = new StripeError { Type = "api_error", Message = "Stripe is down." } };

        var exception = await Assert.ThrowsAsync<StripeException>(
            async () => await _sut.InvokeAsync(_context, _ => throw apiError));

        Assert.Same(apiError, exception);
    }

    [Fact]
    public async Task InvokeAsync_StripeExceptionWithoutErrorDetail_PropagatesStripeException()
    {
        var bare = new StripeException();

        var exception = await Assert.ThrowsAsync<StripeException>(
            async () => await _sut.InvokeAsync(_context, _ => throw bare));

        Assert.Same(bare, exception);
    }
}
