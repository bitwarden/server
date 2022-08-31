using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bit.Api.Health;

[ExcludeFromCodeCoverage]
public class LoggingHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<LoggingHealthCheckPublisher> _logger;

    public LoggingHealthCheckPublisher(ILogger<LoggingHealthCheckPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{ DateTime.Now } Health status: { report.Status }");
            return Task.CompletedTask;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Application is unhealthy.");
        
        Console.ResetColor();
        return Task.CompletedTask;
    }
}
