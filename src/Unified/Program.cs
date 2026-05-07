using Bit.Core.Settings;
using Bit.Unified;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// TODO: Configure global settings
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    // Unified is always ran as self hosted.
    { "GlobalSettings:SelfHosted", "true" },
    // TODO: Remove
    { "GlobalSettings:Installation:Id", Guid.NewGuid().ToString() },
});

IEnumerable<IApplicationConfigurator> services = [
    // Bootstap Identity
    new ApplicationConfigurator<Bit.Identity.Startup>(
        "identity",
        new Bit.Identity.Startup(builder.Environment, builder.Configuration),
        static (startup, builder, services) => startup.ConfigureServices(services),
        static (startup, app, builder) =>
        {
            startup.Configure(
                builder,
                app.Environment,
                app.Services.GetRequiredService<GlobalSettings>(),
                app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Bit.Identity.Startup>()
            );
        }
    ),
    // Bootstrap API
    new ApplicationConfigurator<Bit.Api.Startup>(
        "api",
        new Bit.Api.Startup(builder.Environment, builder.Configuration),
        static (startup, builder, services) => startup.ConfigureServices(services),
        static (startup, app, builder) =>
        {
            startup.Configure(
                builder,
                app.Environment,
                app.Services.GetRequiredService<GlobalSettings>(),
                app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Bit.Api.Startup>()
            );
        }
    ),
    // Bootstrap Admin
    new ApplicationConfigurator<Bit.Admin.Startup>(
        "admin",
        new Bit.Admin.Startup(builder.Environment, builder.Configuration),
        static (startup, builder, services) => startup.ConfigureServices(services),
        static (startup, app, builder) =>
        {
            startup.Configure(
                builder,
                app.Environment,
                app.Services.GetRequiredService<GlobalSettings>()
            );
        }
    )
];

builder.Services.AddOptions<MvcOptions>()
    .Configure<ILoggerFactory>((options, loggerFactory) =>
    {
        options.Conventions.Add(new AssemblyRoutingConvention(
            services,
            loggerFactory.CreateLogger("Bitwarden.Unified")
        ));
    });

// TODO: Place happy path overrides here

foreach (var service in services)
{
    service.ConfigureServices(builder, builder.Services);
}

// TODO: Add overriding services

var app = builder.Build();

var globalSettings = app.Services.GetRequiredService<GlobalSettings>();

// TODO: Middleware
// foreach (var service in services)
// {
//     app.MapWhen(
//         c => c.Request.Path.StartsWithSegments("/" + service.RoutePrefix),
//         builder =>
//         {
//             // Map their specific middleware
//             service.Configure(app, builder);
//         }
//     );
// }

app.MapGet("/endpoints", (EndpointDataSource endpoints, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Bitwarden.Unified");
    foreach (var e in endpoints.Endpoints.OfType<RouteEndpoint>().Select(e => e.RoutePattern.RawText))
    {
        logger.LogWarning("Endpoint: {Route}", e);
    }
    return TypedResults.NoContent();
});

// foreach (var service in services)
// {
//     var group = app.MapGroup("/" + service.RoutePrefix);
//     service.MapEndpoints(group);
// }

app.MapControllers();

app.Run();

public partial class Program;
