using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace Bit.Identity
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebHost
                .CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                // ref: https://github.com/aspnet/KestrelHttpServer/issues/2694
                .UseLibuv()
                .Build()
                .Run();
        }
    }
}
