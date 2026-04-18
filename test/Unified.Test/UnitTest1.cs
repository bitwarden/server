using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.Unified.Test;

public class UnitTest1
{
    [Fact]
    public async Task Test1Async()
    {
        using var app = new UnifiedApplication();

        var client = app.CreateClient();

        var email = $"test+{Guid.NewGuid()}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/identity/accounts/register/send-verification-email", new
        {
            name = "Test User",
            email,
        }, cancellationToken: TestContext.Current.CancellationToken);

        registerResponse.EnsureSuccessStatusCode();

        var token = await registerResponse.Content.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(token);
    }
}

public class UnifiedApplication : WebApplicationFactory<Program>
{
    public IMailService MockMailService { get; } = Substitute.For<IMailService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(MockMailService);
        });

        builder.ConfigureAppConfiguration((context, builder) =>
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GlobalSettings:EnableEmailVerification", "false" },
                { "Logging:LogLevel:Duende", "Warning" },
                { "Logging:LogLevel:Bitwarden.Unified", "Debug" },
                { "Logging:LogLevel:Microsoft.AspNetCore.Routing", "Debug" },
            });
        });
    }
}
