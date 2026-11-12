using Bit.Core.KeyManagement.Sends;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class SendPasswordHasherServiceCollectionExtensions
{
    public static void AddSendPasswordServices(this IServiceCollection services)
    {
        services.AddServerPasswordHasher();
        services.TryAddScoped<ISendPasswordHasher, SendPasswordHasher>();
    }
}
