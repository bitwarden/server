using System.Globalization;
using Bit.Core.Settings;
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
        services.AddOptions();

        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);

        services.AddCustomDataProtectionServices(Environment, globalSettings);

        services.AddTokenizers();
        services.AddDatabaseRepositories(globalSettings);

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddScoped<IPasswordHasher<Core.Entities.User>, PasswordHasher<Core.Entities.User>>();

        services.AddSingleton<RustSDK.RustSdkService>();
        services.AddScoped<UserSeeder>();
        services.AddScoped<ISceneService, SceneService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<MangleId>(_ => new MangleId());
        services.AddScenes();
        services.AddQueries();

        services.AddControllers();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        if (env.IsProduction())
        {
            throw new InvalidOperationException(
                "SeederApi cannot be run in production environments. This service is intended for test data generation only.");
        }

        if (globalSettings.TestPlayIdTrackingEnabled)
        {
            app.UseMiddleware<PlayIdMiddleware>();
        }

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
