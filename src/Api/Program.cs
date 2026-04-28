using Bit.Core.Utilities;

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

        builder.Build().Run();
    }
}
