using System.Globalization;
using Bit.Core;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;

namespace Bit.EventsProcessor
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IConfiguration configuration)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            services.AddGlobalSettingsServices(Configuration);

            // Hosted Services
            services.AddHostedService<AzureQueueHostedService>();
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            IdentityModelEventSource.ShowPII = true;
            app.UseSerilog(env, appLifetime, globalSettings);
        }
    }
}
