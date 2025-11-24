using Bit.Core.Utilities;

namespace Bit.Icons;

public class Program
{
    public static void Main(string[] args)
    {
        Host
            .CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .AddSerilogFileLogging()
            .Build()
            .Run();
    }
}
