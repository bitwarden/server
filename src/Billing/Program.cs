using Bit.Core.Utilities;

namespace Bit.Billing;

public class Program
{
    public static void Main(string[] args)
    {
        RegexDefaults.ConfigureDefaultMatchTimeout();

        Host
            .CreateDefaultBuilder(args)
            .UseBitwardenSdk()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .AddSerilogFileLogging()
            .Build()
            .Run();
    }
}
