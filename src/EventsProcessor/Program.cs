using Bit.Core.Utilities;

namespace Bit.EventsProcessor;

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
