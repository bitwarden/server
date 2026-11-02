using Bit.Core.Utilities;

namespace Bit.Notifications;

public class Program
{
    public static void Main(string[] args)
    {
        // This project does not reference Core, so this mirrors Bit.Core.Utilities.RegexDefaults:
        // a process-wide Regex match timeout as defence in depth against catastrophic
        // backtracking (CWE-1333). It must run before any Regex is constructed.
        AppContext.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(5));

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
