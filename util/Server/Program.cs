using Microsoft.AspNetCore.Hosting;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>();

            if(args.Length > 0)
            {
                builder.UseContentRoot(args[0]);
            }

            if(args.Length > 1)
            {
                builder.UseWebRoot(args[1]);
            }

            var host = builder.Build();
            host.Run();
        }
    }
}
