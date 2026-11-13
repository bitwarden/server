using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bit.Events.IntegrationTest;

public class StartingEventsTests
{
    [Fact]
    public void DoesNotRegisterDataProtection()
    {
        using var factory = new EventsApplicationFactory();

        Assert.DoesNotContain(factory.Services.GetServices<IHostedService>(),
            hostedService => hostedService.GetType().Namespace?.StartsWith("Microsoft.AspNetCore.DataProtection") == true);
    }
}
