using System.Globalization;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Seeder;
using Bit.Seeder.Factories;
using Bit.SeederApi.Extensions;
using Bit.SeederApi.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.SeederApi;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; private set; }
    public IWebHostEnvironment Environment { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddTokenizers();
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Identity
        services.AddScoped<IPasswordHasher<Core.Entities.User>, PasswordHasher<Core.Entities.User>>();

        // Seeder services
        services.AddSingleton<RustSDK.RustSdkService>();
        services.AddScoped<UserSeeder>();
        services.AddScoped<ISceneService, SceneService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<MangleId>(_ => new MangleId());
        services.AddScenes();
        services.AddQueries();

        // MVC
        services.AddControllers();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        app.UseSerilog(env, appLifetime, globalSettings);

        // Add PlayIdMiddleware services
        if (globalSettings.TestPlayIdTrackingEnabled)
        {
            app.UseMiddleware<PlayIdMiddleware>();
        }

        // Configure the HTTP request pipeline
        if (!env.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
        }

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(name: "default", pattern: "{controller=Seed}/{action=Index}/{id?}");
        });
    }
}
