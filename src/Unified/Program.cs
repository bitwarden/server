using Bit.Core.Settings;
using Bit.Unified;

var builder = WebApplication.CreateBuilder(args);

// TODO: Configure global settings
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    // Unified is always ran as self hosted.
    {"GlobalSettings:SelfHosted", "true"},
    // TODO: Remove
    {"GlobalSettings:Installation:Id", Guid.NewGuid().ToString() },
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
                app.Lifetime,
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
                app.Lifetime,
                app.Services.GetRequiredService<GlobalSettings>(),
                app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Bit.Api.Startup>()
            );
        }
    ),
];

var mvcBuilder = builder.Services.AddMvcCore(options =>
{
    options.Conventions.Add(new AssemblyRoutingConvention(services));
});

// foreach (var service in services)
// {
//     // Make sure the controllers for this service are available
//     mvcBuilder.AddApplicationPart(service.GetType().Assembly);
// }

// TODO: Place happy path overrides here

foreach (var service in services)
{
    service.ConfigureServices(builder, builder.Services);
}

// TODO: Add overriding services

var app = builder.Build();

var globalSettings = app.Services.GetRequiredService<GlobalSettings>();

// TODO: Middleware
foreach (var service in services)
{
    app.MapWhen(
        c => c.Request.Path.StartsWithSegments("/" + service.RoutePrefix), 
        builder =>
        {
            // Map their specific middleware
            service.Configure(app, builder);
        }
    );
}

// foreach (var service in services)
// {
//     var group = app.MapGroup("/" + service.RoutePrefix);
//     service.MapEndpoints(group);
// }

// TODO: Test a controller that is 100% convention based
app.MapControllerRoute(
    "default",
    "{prefix}/{controller=Home}/{action=Index}/{id?}"
);

app.Run();
