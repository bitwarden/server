using Bit.Core.Utilities;
#if DEBUG
using Bit.ServiceDefaults;
#endif

namespace Bit.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host
            .CreateDefaultBuilder(args)
            .UseBitwardenSdk()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .AddSerilogFileLogging();

#if DEBUG
        builder.AddServiceDefaults();
#endif

        builder.Build().Run();
    }
}
