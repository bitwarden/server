using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bit.IntegrationTestCommon;

public static class StartupTestHelpers
{
    /// <summary>
    /// Starts the host of <paramref name="factory"/> in <paramref name="environment"/>, and asserts that startup
    /// fails with a message that contains <paramref name="expectedMessageFragment"/>.
    /// </summary>
    public static Exception AssertStartupFails<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory,
        string environment, string expectedMessageFragment)
        where TEntryPoint : class
    {
        using var environmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        var exception = Assert.ThrowsAny<Exception>(() => environmentFactory.CreateClient());

        Assert.Contains(expectedMessageFragment, exception.Message);
        return exception;
    }
}
