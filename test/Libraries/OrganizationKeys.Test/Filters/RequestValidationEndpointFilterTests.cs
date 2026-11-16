using Bit.Core.Exceptions;
using Bit.OrganizationKeys.Filters;
using Bit.OrganizationKeys.Models.Requests;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bit.OrganizationKeys.Test.Filters;

public class RequestValidationEndpointFilterTests
{
    private const string _organizationKeyId = "0123456789abcdef0123456789abcdef";

    private readonly RequestValidationEndpointFilter<SetOrganizationKeyIdRequest> _sut = new();

    [Fact]
    public async Task InvokeAsync_ValidRequest_ReturnsResultFromNext()
    {
        var expected = new object();

        var result = await _sut.InvokeAsync(ContextFor(_organizationKeyId),
            _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-key-id")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    public async Task InvokeAsync_InvalidKeyId_ThrowsModelStateErrorWithoutCallingNext(string organizationKeyId)
    {
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await _sut.InvokeAsync(ContextFor(organizationKeyId), _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>(null);
            }));

        Assert.NotNull(exception.ModelState);
        Assert.True(exception.ModelState!.ContainsKey(nameof(SetOrganizationKeyIdRequest.OrganizationKeyId)));
        Assert.False(nextCalled);
    }

    // Matches the endpoint's argument list: the Guid route value, then the request.
    private static EndpointFilterInvocationContext ContextFor(string organizationKeyId) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), Guid.NewGuid(),
            new SetOrganizationKeyIdRequest { OrganizationKeyId = organizationKeyId });
}
