using Bit.Api;
using Bit.Core.Arch;
using Bit.Unified;

var builder = WebApplication.CreateBuilder(args);

// TODO: Configure global settings

IEnumerable<BitService> services = [
    new ApiService(),
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

var app = builder.Build();

// TODO: Middleware
foreach (var service in services)
{
    app.MapWhen(c => c.Request.Path.StartsWithSegments(service.RoutePrefix), builder =>
    {
        // Map their specific middleware
    });
}

foreach (var service in services)
{
    var group = app.MapGroup("/" + service.RoutePrefix);
    service.MapEndpoints(group);
}

// TODO: Test a controller that is 100% convention based
app.MapControllerRoute(
    "default",
    "{prefix}/{controller=Home}/{action=Index}/{id?}"
);



app.Run();
