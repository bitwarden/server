using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using IdentityModel;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using System;
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
            services.AddSingleton<IFolderRepository, SqlServerRepos.FolderRepository>();
            services.AddSingleton<ICollectionCipherRepository, SqlServerRepos.CollectionCipherRepository>();
            services.AddSingleton<IGroupRepository, SqlServerRepos.GroupRepository>();
            services.AddSingleton<IU2fRepository, SqlServerRepos.U2fRepository>();
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
            services.AddSingleton<IMailService, RazorViewMailService>();
            services.AddSingleton<IMailDeliveryService, SendGridMailDeliveryService>();
            services.AddSingleton<IPushNotificationService, NotificationHubPushNotificationService>();
            services.AddSingleton<IBlockIpService, AzureQueueBlockIpService>();
            services.AddSingleton<IPushRegistrationService, NotificationHubPushRegistrationService>();
            services.AddSingleton<IAttachmentStorageService, AzureAttachmentStorageService>();
        }

        public static void AddNoopServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailService, NoopMailService>();
            services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();
            services.AddSingleton<IPushNotificationService, NoopPushNotificationService>();
            services.AddSingleton<IBlockIpService, NoopBlockIpService>();
            services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
            services.AddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
        }

        public static IdentityBuilder AddCustomIdentityServices(
            this IServiceCollection services, GlobalSettings globalSettings)
        {
            services.AddTransient<ILookupNormalizer, LowerInvariantLookupNormalizer>();

            services.Configure<TwoFactorRememberTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromDays(30);
            });

            var identityBuilder = services.AddIdentity<User, Role>(options =>
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
                    SecurityStampClaimType = "sstamp",
                    UserNameClaimType = JwtClaimTypes.Email,
                    UserIdClaimType = JwtClaimTypes.Subject,
                };
                options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
            });

            identityBuilder
                .AddUserStore<UserStore>()
                .AddRoleStore<RoleStore>()
                .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenOptions.DefaultProvider)
                .AddTokenProvider<AuthenticatorTokenProvider>(TwoFactorProviderType.Authenticator.ToString())
                .AddTokenProvider<YubicoOtpTokenProvider>(TwoFactorProviderType.YubiKey.ToString())
                .AddTokenProvider<DuoWebTokenProvider>(TwoFactorProviderType.Duo.ToString())
                .AddTokenProvider<U2fTokenProvider>(TwoFactorProviderType.U2f.ToString())
                .AddTokenProvider<TwoFactorRememberTokenProvider>(TwoFactorProviderType.Remember.ToString())
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

            if(env.IsDevelopment())
            {
                identityServerBuilder.AddTemporarySigningCredential();
            }
            else
            {
                var identityServerCert = CoreHelpers.GetCertificate(globalSettings.IdentityServer.CertificateThumbprint);
                identityServerBuilder.AddSigningCredential(identityServerCert);
            }

            services.AddScoped<IResourceOwnerPasswordValidator, ResourceOwnerPasswordValidator>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddSingleton<IPersistedGrantStore, PersistedGrantStore>();

            return identityServerBuilder;
        }

        public static void AddCustomDataProtectionServices(
            this IServiceCollection services, IHostingEnvironment env, GlobalSettings globalSettings)
        {
            if(!env.IsDevelopment())
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
