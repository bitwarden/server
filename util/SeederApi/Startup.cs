using System.Globalization;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.SeederApi.Extensions;
using Bit.SeederApi.Utilities;
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
        services.AddTestPlayIdTracking(globalSettings);
        services.AddManglerService(globalSettings);

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddScoped<IPasswordHasher<Core.Entities.User>, PasswordHasher<Core.Entities.User>>();

        // License infrastructure — needed to write premium license files for self-hosted validation.
        // SingleUserScene takes ILicensingService as a required dependency, so this is always
        // registered here. SeederApi refuses to run in production (see Configure), so the
        // self-hosted Installation Id requirement enforced by AddPush is acceptable.
        services.AddLicenseServices();
        services.TryAddSingleton<IMailService, NoopMailService>();
        services.TryAddSingleton<IPushNotificationService, MultiServicePushNotificationService>();
        services.TryAddSingleton<ILicensingService, LicensingService>();

        services.AddSeederApiServices();
        services.AddScenes();
        services.AddQueries();

        services.Configure<SeederSettings>(Configuration.GetSection("seederSettings"));

        services.AddAuthentication(BasicAuthenticationOptions.DefaultScheme)
            .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
                BasicAuthenticationOptions.DefaultScheme, null);

        services.AddAuthorization();

        services.AddControllers();

        Jobs.JobsHostedService.AddJobsServices(services);
        services.AddHostedService<Jobs.JobsHostedService>();
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
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(name: "default", pattern: "{controller=Seed}/{action=Index}/{id?}");
        });
    }
}
