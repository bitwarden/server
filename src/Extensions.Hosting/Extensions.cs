using Microsoft.Extensions.Hosting;

namespace Bit.Extensions.Hosting;

public static class Extensions
{
    public static void UseBitwardenDefaults(
        this IHostBuilder hostBuilder)
    {
        hostBuilder.UseBitwardenLogging();
    }
}
