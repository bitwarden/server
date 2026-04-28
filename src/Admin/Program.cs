using Bit.Core.Utilities;

namespace Bit.Admin;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host
            .CreateDefaultBuilder(args)
            .UseBitwardenSdk()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(o =>
                {
                    o.Limits.MaxRequestLineSize = 20_000;
                });
                webBuilder.UseStartup<Startup>();
            })
            .AddSerilogFileLogging();

        builder.Build().Run();
    }
}
