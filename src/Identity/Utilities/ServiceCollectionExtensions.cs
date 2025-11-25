using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.SharedWeb.Utilities;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;

namespace Bit.Identity.Utilities;

public static class ServiceCollectionExtensions
{
    public static IIdentityServerBuilder AddCustomIdentityServerServices(this IServiceCollection services,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        services.AddTransient<IDiscoveryResponseGenerator, DiscoveryResponseGenerator>();

        services.AddSingleton<StaticClientStore>();
        services.AddTransient<IAuthorizationCodeStore, AuthorizationCodeStore>();
        services.AddTransient<IUserDecryptionOptionsBuilder, UserDecryptionOptionsBuilder>();
        services.AddTransient<IDeviceValidator, DeviceValidator>();
        services.AddTransient<IClientVersionValidator, ClientVersionValidator>();
        services.AddTransient<ITwoFactorAuthenticationValidator, TwoFactorAuthenticationValidator>();
        services.AddTransient<ILoginApprovingClientTypes, LoginApprovingClientTypes>();
        services.AddTransient<ISendAuthenticationMethodValidator<ResourcePassword>, SendPasswordRequestValidator>();
        services.AddTransient<ISendAuthenticationMethodValidator<EmailOtp>, SendEmailOtpRequestValidator>();
        services.AddTransient<ISendAuthenticationMethodValidator<NeverAuthenticate>, SendNeverAuthenticateRequestValidator>();

        var issuerUri = new Uri(globalSettings.BaseServiceUri.InternalIdentity);
        var identityServerBuilder = services
            .AddIdentityServer(options =>
            {
                options.LicenseKey = globalSettings.IdentityServer.LicenseKey;
                options.Endpoints.EnableIntrospectionEndpoint = false;
                options.Endpoints.EnableEndSessionEndpoint = false;
                options.Endpoints.EnableUserInfoEndpoint = false;
                options.Endpoints.EnableCheckSessionEndpoint = false;
                options.Endpoints.EnableTokenRevocationEndpoint = false;
                options.IssuerUri = $"{issuerUri.Scheme}://{issuerUri.Host}";
                options.Caching.ClientStoreExpiration = new TimeSpan(0, 5, 0);
                if (env.IsDevelopment())
                {
                    options.Authentication.CookieSameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                }
                options.InputLengthRestrictions.UserName = 256;
                options.KeyManagement.Enabled = false;
                options.UserInteraction.LoginUrl = "/sso/Login";
            })
            .AddInMemoryCaching()
            .AddInMemoryApiResources(ApiResources.GetApiResources())
            .AddInMemoryApiScopes(ApiScopes.GetApiScopes())
            .AddClientStoreCache<DynamicClientStore>()
            .AddCustomTokenRequestValidator<CustomTokenRequestValidator>()
            .AddProfileService<ProfileService>()
            .AddResourceOwnerValidator<ResourceOwnerPasswordValidator>()
            .AddClientStore<DynamicClientStore>()
            .AddIdentityServerCertificate(env, globalSettings)
            .AddExtensionGrantValidator<WebAuthnGrantValidator>()
            .AddExtensionGrantValidator<SendAccessGrantValidator>();

        if (!globalSettings.SelfHosted)
        {
            // Only cloud instances should be able to handle installations
            services.AddClientProvider<InstallationClientProvider>("installation");
        }

        if (globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.InternalIdentityKey))
        {
            services.AddClientProvider<InternalClientProvider>("internal");
        }

        services.AddClientProvider<UserClientProvider>("user");
        services.AddClientProvider<OrganizationClientProvider>("organization");
        services.AddClientProvider<SecretsManagerApiKeyProvider>(SecretsManagerApiKeyProvider.ApiKeyPrefix);

        if (CoreHelpers.SettingHasValue(globalSettings.IdentityServer.CosmosConnectionString))
        {
            services.AddSingleton<IPersistedGrantStore>(sp =>
                new PersistedGrantStore(sp.GetRequiredKeyedService<IGrantRepository>("cosmos"),
                    g => new Core.Auth.Models.Data.GrantItem(g)));
        }
        else
        {
            services.AddTransient<IPersistedGrantStore>(sp =>
                new PersistedGrantStore(sp.GetRequiredService<IGrantRepository>(),
                    g => new Core.Auth.Entities.Grant(g)));
        }

        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();
        return identityServerBuilder;
    }
}
