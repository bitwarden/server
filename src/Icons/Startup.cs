using System.Globalization;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Icons.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.Net.Http.Headers;

namespace Bit.Icons;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);
        var iconsSettings = new IconsSettings();
        ConfigurationBinder.Bind(Configuration.GetSection("IconsSettings"), iconsSettings);
        services.AddSingleton(s => iconsSettings);

        // Cache
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = iconsSettings.CacheSizeLimit;
        });

        // Services
        services.AddSingleton<IDomainMappingService, DomainMappingService>();
        services.AddSingleton<IIconFetchingService, IconFetchingService>();

        // Mvc
        services.AddMvc();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        app.UseSerilog(env, appLifetime, globalSettings);

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.Use(async (context, next) =>
        {
            context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(7)
            };
            await next();
        });

        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
