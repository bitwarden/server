using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Bit.Extensions.Hosting;

public static class LoggerExtensions
{
    public static void UseBitwardenLogging(
        this IHostBuilder hostBuilder)
    {
        // TODO
    }
}
