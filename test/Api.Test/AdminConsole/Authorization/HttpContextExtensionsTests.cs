using Bit.Api.AdminConsole.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class HttpContextExtensionsTests
{
    [Fact]
    public async Task WithFeaturesCacheAsync_OnlyExecutesCallbackOnce()
    {
        var httpContext = new DefaultHttpContext();
        var callback = Substitute.For<Func<Task<string>>>();
        callback().Returns(Task.FromResult("hello world"));

        // Call once
        var result1 = await httpContext.WithFeaturesCacheAsync(callback);
        Assert.Equal("hello world", result1);
        await callback.ReceivedWithAnyArgs(1).Invoke();

        // Call again - callback not executed again
        var result2 = await httpContext.WithFeaturesCacheAsync(callback);
        Assert.Equal("hello world", result2);
        await callback.ReceivedWithAnyArgs(1).Invoke();
    }
}
