using Bit.Core.Utilities;

namespace Bit.Sso;

public class Program
{
    public static void Main(string[] args)
    {
        RegexDefaults.ConfigureDefaultMatchTimeout();

        Host
            .CreateDefaultBuilder(args)
            .UseBitwardenSdk()
            .ConfigureCustomAppConfiguration(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .AddSerilogFileLogging()
            .Build()
            .Run();
    }
}
