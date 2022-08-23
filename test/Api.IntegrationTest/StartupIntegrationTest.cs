using Bit.Core.Settings;

namespace Bit.Api
{
    public class StartupIntegrationTest : Startup
    {
        public StartupIntegrationTest(IWebHostEnvironment env, IConfiguration configuration) : base(env, configuration)
        {
        }

        protected override void AddIdentity(IServiceCollection services, GlobalSettings globalSettings)
        {
            services.AddAuthorization(BuildAuthorizationOptions);
        }
    }
}
