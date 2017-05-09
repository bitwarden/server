using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Security.Claims;
using System.Text;
using SqlServerRepos = Bit.Core.Repositories.SqlServer;

namespace Bit.Core.Utilities
{
    public static class ServiceCollectionExtensions
    {
        public static void AddSqlServerRepositories(this IServiceCollection services)
        {
            services.AddSingleton<IUserRepository, SqlServerRepos.UserRepository>();
            services.AddSingleton<ICipherRepository, SqlServerRepos.CipherRepository>();
            services.AddSingleton<IDeviceRepository, SqlServerRepos.DeviceRepository>();
            services.AddSingleton<IGrantRepository, SqlServerRepos.GrantRepository>();
            services.AddSingleton<IOrganizationRepository, SqlServerRepos.OrganizationRepository>();
            services.AddSingleton<IOrganizationUserRepository, SqlServerRepos.OrganizationUserRepository>();
            services.AddSingleton<ICollectionRepository, SqlServerRepos.CollectionRepository>();
            services.AddSingleton<ICollectionUserRepository, SqlServerRepos.CollectionUserRepository>();
            services.AddSingleton<IFolderRepository, SqlServerRepos.FolderRepository>();
            services.AddSingleton<ICollectionCipherRepository, SqlServerRepos.CollectionCipherRepository>();
            services.AddSingleton<IGroupRepository, SqlServerRepos.GroupRepository>();
        }

        public static void AddBaseServices(this IServiceCollection services)
        {
            services.AddSingleton<ICipherService, CipherService>();
            services.AddScoped<IUserService, UserService>();
            services.AddSingleton<IDeviceService, DeviceService>();
            services.AddSingleton<IOrganizationService, OrganizationService>();
            services.AddSingleton<ICollectionService, CollectionService>();
            services.AddSingleton<IGroupService, GroupService>();
        }

        public static void AddDefaultServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailService, SendGridMailService>();
            services.AddSingleton<IPushService, PushSharpPushService>();
            services.AddSingleton<IBlockIpService, AzureQueueBlockIpService>();
        }

        public static void AddNoopServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailService, NoopMailService>();
            services.AddSingleton<IPushService, NoopPushService>();
            services.AddSingleton<IBlockIpService, NoopBlockIpService>();
        }

        public static IdentityBuilder AddCustomIdentityServices(
            this IServiceCollection services, GlobalSettings globalSettings)
        {
            services.AddTransient<ILookupNormalizer, LowerInvariantLookupNormalizer>();

            var identityBuilder = services.AddJwtBearerIdentity(options =>
            {
                options.User = new UserOptions
                {
                    RequireUniqueEmail = true,
                    AllowedUserNameCharacters = null // all
                };
                options.Password = new PasswordOptions
                {
                    RequireDigit = false,
                    RequireLowercase = false,
                    RequiredLength = 8,
                    RequireNonAlphanumeric = false,
                    RequireUppercase = false
                };
                options.ClaimsIdentity = new ClaimsIdentityOptions
                {
                    SecurityStampClaimType = "securitystamp",
                    UserNameClaimType = ClaimTypes.Email
                };
                options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
            }, jwtBearerOptions =>
            {
                jwtBearerOptions.Audience = "bitwarden";
                jwtBearerOptions.Issuer = "bitwarden";
                jwtBearerOptions.TokenLifetime = TimeSpan.FromDays(10 * 365);
                jwtBearerOptions.TwoFactorTokenLifetime = TimeSpan.FromMinutes(10);
                var keyBytes = Encoding.ASCII.GetBytes(globalSettings.JwtSigningKey);
                jwtBearerOptions.SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            });

            identityBuilder
                .AddUserStore<UserStore>()
                .AddRoleStore<RoleStore>()
                .AddTokenProvider<AuthenticatorTokenProvider>(TwoFactorProviderType.Authenticator.ToString())
                .AddTokenProvider<EmailTokenProvider<User>>(TokenOptions.DefaultEmailProvider);

            return identityBuilder;
        }

        public static IIdentityServerBuilder AddCustomIdentityServerServices(
            this IServiceCollection services, IHostingEnvironment env, GlobalSettings globalSettings)
        {
            var identityServerBuilder = services
                .AddIdentityServer(options =>
                {
                    options.Endpoints.EnableAuthorizeEndpoint = false;
                    options.Endpoints.EnableIntrospectionEndpoint = false;
                    options.Endpoints.EnableEndSessionEndpoint = false;
                    options.Endpoints.EnableUserInfoEndpoint = false;
                    options.Endpoints.EnableCheckSessionEndpoint = false;
                    options.Endpoints.EnableTokenRevocationEndpoint = false;
                })
                .AddInMemoryApiResources(ApiResources.GetApiResources())
                .AddInMemoryClients(Clients.GetClients());

            services.AddTransient<ICorsPolicyService, AllowAllCorsPolicyService>();

            if(env.IsProduction())
            {
                var identityServerCert = CoreHelpers.GetCertificate(globalSettings.IdentityServer.CertificateThumbprint);
                identityServerBuilder.AddSigningCredential(identityServerCert);
            }
            else
            {
                identityServerBuilder.AddTemporarySigningCredential();
            }

            services.AddScoped<IResourceOwnerPasswordValidator, ResourceOwnerPasswordValidator>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddSingleton<IPersistedGrantStore, PersistedGrantStore>();

            return identityServerBuilder;
        }

        public static void AddCustomDataProtectionServices(
            this IServiceCollection services, IHostingEnvironment env, GlobalSettings globalSettings)
        {
            if(env.IsProduction())
            {
                var dataProtectionCert = CoreHelpers.GetCertificate(globalSettings.DataProtection.CertificateThumbprint);
                var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
                services.AddDataProtection()
                    .PersistKeysToAzureBlobStorage(storageAccount, "aspnet-dataprotection/keys.xml")
                    .ProtectKeysWithCertificate(dataProtectionCert);
            }
        }

        public static GlobalSettings AddGlobalSettingsServices(this IServiceCollection services,
            IConfigurationRoot root)
        {
            var globalSettings = new GlobalSettings();
            ConfigurationBinder.Bind(root.GetSection("GlobalSettings"), globalSettings);
            services.AddSingleton(s => globalSettings);
            return globalSettings;
        }
    }
}
