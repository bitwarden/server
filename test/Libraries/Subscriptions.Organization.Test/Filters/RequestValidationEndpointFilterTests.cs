using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Subscriptions.Organization.Filters;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bit.Subscriptions.Organization.Test.Filters;

public class RequestValidationEndpointFilterTests
{
    private readonly RequestValidationEndpointFilter<TestRequest> _sut = new();

    [Fact]
    public async Task InvokeAsync_ValidRequest_ReturnsResultFromNext()
    {
        var expected = new object();

        var result = await _sut.InvokeAsync(ContextFor(new TestRequest { Count = 5 }),
            _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_InvalidProperty_ThrowsBadRequestWithTheMessageKeyedByProperty()
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await _sut.InvokeAsync(ContextFor(new TestRequest { Count = 0 }), _ => ValueTask.FromResult<object?>(null)));

        Assert.Equal(["Count must be between 1 and 10"],
            exception.ModelState![nameof(TestRequest.Count)]!.Errors.Select(error => error.ErrorMessage));
    }

    [Fact]
    public async Task InvokeAsync_FailureWithoutAProperty_ThrowsBadRequestWithTheMessageUnderAnEmptyKey()
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await _sut.InvokeAsync(ContextFor(new TestRequest { Count = 5, RejectWholeRequest = true }),
                _ => ValueTask.FromResult<object?>(null)));

        Assert.Equal(["The request is not allowed"],
            exception.ModelState![string.Empty]!.Errors.Select(error => error.ErrorMessage));
    }

    private static EndpointFilterInvocationContext ContextFor(TestRequest request) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), Guid.NewGuid(), request);

    private sealed class TestRequest : IValidatableObject
    {
        [Range(1, 10, ErrorMessage = "Count must be between 1 and 10")]
        public int Count { get; init; }

        public bool RejectWholeRequest { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (RejectWholeRequest)
            {
                yield return new ValidationResult("The request is not allowed");
            }
        }
    }
}
