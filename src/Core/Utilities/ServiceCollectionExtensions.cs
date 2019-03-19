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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.WindowsAzure.Storage;
using System;
using System.IO;
using SqlServerRepos = Bit.Core.Repositories.SqlServer;
using PostgreSqlRepos = Bit.Core.Repositories.PostgreSql;
using NoopRepos = Bit.Core.Repositories.Noop;
using System.Threading.Tasks;
using TableStorageRepos = Bit.Core.Repositories.TableStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IdentityServer4.AccessTokenValidation;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Utilities
{
    public static class ServiceCollectionExtensions
    {
        public static void AddSqlServerRepositories(this IServiceCollection services, GlobalSettings globalSettings)
        {
            if(!string.IsNullOrWhiteSpace(globalSettings.PostgreSql?.ConnectionString))
            {
                services.AddSingleton<IUserRepository, PostgreSqlRepos.UserRepository>();
            }
            else
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
                services.AddSingleton<IInstallationRepository, SqlServerRepos.InstallationRepository>();
                services.AddSingleton<IMaintenanceRepository, SqlServerRepos.MaintenanceRepository>();
                services.AddSingleton<ITransactionRepository, SqlServerRepos.TransactionRepository>();
            }

            if(globalSettings.SelfHosted)
            {
                services.AddSingleton<IEventRepository, SqlServerRepos.EventRepository>();
                services.AddSingleton<IInstallationDeviceRepository, NoopRepos.InstallationDeviceRepository>();
            }
            else
            {
                services.AddSingleton<IEventRepository, TableStorageRepos.EventRepository>();
                services.AddSingleton<IInstallationDeviceRepository, TableStorageRepos.InstallationDeviceRepository>();
            }
        }

        public static void AddBaseServices(this IServiceCollection services)
        {
            services.AddScoped<ICipherService, CipherService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<ICollectionService, CollectionService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<Services.IEventService, EventService>();
            services.AddSingleton<IDeviceService, DeviceService>();
        }

        public static void AddDefaultServices(this IServiceCollection services, GlobalSettings globalSettings)
        {
            services.AddSingleton<IPaymentService, StripePaymentService>();
            services.AddSingleton<IMailService, HandlebarsMailService>();
            services.AddSingleton<ILicensingService, LicensingService>();
            services.AddSingleton<IApplicationCacheService, InMemoryApplicationCacheService>();

            if(CoreHelpers.SettingHasValue(globalSettings.Mail.SendGridApiKey))
            {
                services.AddSingleton<IMailDeliveryService, SendGridMailDeliveryService>();
            }
            else if(CoreHelpers.SettingHasValue(globalSettings.Amazon?.AccessKeySecret))
            {
                services.AddSingleton<IMailDeliveryService, AmazonSesMailDeliveryService>();
            }
            else if(CoreHelpers.SettingHasValue(globalSettings.Mail?.Smtp?.Host))
            {
                services.AddSingleton<IMailDeliveryService, MailKitSmtpMailDeliveryService>();
            }
            else
            {
                services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();
            }

            services.AddSingleton<IPushNotificationService, MultiServicePushNotificationService>();
            if(globalSettings.SelfHosted &&
                CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
                globalSettings.Installation?.Id != null &&
                CoreHelpers.SettingHasValue(globalSettings.Installation?.Key))
            {
                services.AddSingleton<IPushRegistrationService, RelayPushRegistrationService>();
            }
            else if(!globalSettings.SelfHosted)
            {
                services.AddSingleton<IPushRegistrationService, NotificationHubPushRegistrationService>();
            }
            else
            {
                services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
            }

            if(!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Storage.ConnectionString))
            {
                services.AddSingleton<IBlockIpService, AzureQueueBlockIpService>();
            }
            else if(!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Amazon?.AccessKeySecret))
            {
                services.AddSingleton<IBlockIpService, AmazonSqsBlockIpService>();
            }
            else
            {
                services.AddSingleton<IBlockIpService, NoopBlockIpService>();
            }

            if(!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Events.ConnectionString))
            {
                services.AddSingleton<IEventWriteService, AzureQueueEventWriteService>();
            }
            else if(globalSettings.SelfHosted)
            {
                services.AddSingleton<IEventWriteService, RepositoryEventWriteService>();
            }
            else
            {
                services.AddSingleton<IEventWriteService, NoopEventWriteService>();
            }

            if(CoreHelpers.SettingHasValue(globalSettings.Attachment.ConnectionString))
            {
                services.AddSingleton<IAttachmentStorageService, AzureAttachmentStorageService>();
            }
            else if(CoreHelpers.SettingHasValue(globalSettings.Attachment.BaseDirectory))
            {
                services.AddSingleton<IAttachmentStorageService, LocalAttachmentStorageService>();
            }
            else
            {
                services.AddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
            }
        }

        public static void AddNoopServices(this IServiceCollection services)
        {
            services.AddSingleton<IMailService, NoopMailService>();
            services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();
            services.AddSingleton<IPushNotificationService, NoopPushNotificationService>();
            services.AddSingleton<IBlockIpService, NoopBlockIpService>();
            services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
            services.AddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
            services.AddSingleton<ILicensingService, NoopLicensingService>();
            services.AddSingleton<IEventWriteService, NoopEventWriteService>();
        }

        public static IdentityBuilder AddCustomIdentityServices(
            this IServiceCollection services, GlobalSettings globalSettings)
        {
            services.AddSingleton<IOrganizationDuoWebTokenProvider, OrganizationDuoWebTokenProvider>();
            services.Configure<PasswordHasherOptions>(options => options.IterationCount = 100000);
            services.Configure<TwoFactorRememberTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromDays(30);
            });

            var identityBuilder = services.AddIdentityWithoutCookieAuth<User, Role>(options =>
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
                    UserIdClaimType = JwtClaimTypes.Subject
                };
                options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
            });

            identityBuilder
                .AddUserStore<UserStore>()
                .AddRoleStore<RoleStore>()
                .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenOptions.DefaultProvider)
                .AddTokenProvider<AuthenticatorTokenProvider>(
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.Authenticator))
                .AddTokenProvider<EmailTokenProvider>(
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.Email))
                .AddTokenProvider<YubicoOtpTokenProvider>(
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.YubiKey))
                .AddTokenProvider<DuoWebTokenProvider>(
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.Duo))
                .AddTokenProvider<U2fTokenProvider>(
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.U2f))
                .AddTokenProvider<TwoFactorRememberTokenProvider>(
                    CoreHelpers.CustomProviderName(TwoFactorProviderType.Remember))
                .AddTokenProvider<EmailTokenProvider<User>>(TokenOptions.DefaultEmailProvider);

            return identityBuilder;
        }

        public static Tuple<IdentityBuilder, IdentityBuilder> AddPasswordlessIdentityServices<TUserStore>(
            this IServiceCollection services, GlobalSettings globalSettings) where TUserStore : class
        {
            services.TryAddTransient<ILookupNormalizer, LowerInvariantLookupNormalizer>();
            services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromMinutes(15);
            });

            var passwordlessIdentityBuilder = services.AddIdentity<IdentityUser, Role>()
                .AddUserStore<TUserStore>()
                .AddRoleStore<RoleStore>()
                .AddDefaultTokenProviders();

            var regularIdentityBuilder = services.AddIdentityCore<User>()
                .AddUserStore<UserStore>();

            services.TryAddScoped<PasswordlessSignInManager<IdentityUser>, PasswordlessSignInManager<IdentityUser>>();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/";
                options.AccessDeniedPath = "/login?accessDenied=true";
                options.Cookie.Name = $"Bitwarden_{globalSettings.ProjectName}";
                options.Cookie.HttpOnly = true;
                options.Cookie.Expiration = options.ExpireTimeSpan = TimeSpan.FromDays(2);
                options.ReturnUrlParameter = "returnUrl";
                options.SlidingExpiration = true;
            });

            return new Tuple<IdentityBuilder, IdentityBuilder>(passwordlessIdentityBuilder, regularIdentityBuilder);
        }

        public static void AddIdentityAuthenticationServices(
            this IServiceCollection services, GlobalSettings globalSettings, IHostingEnvironment environment,
            Action<AuthorizationOptions> addAuthorization)
        {
            services
                .AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
                .AddIdentityServerAuthentication(options =>
                {
                    options.Authority = globalSettings.BaseServiceUri.InternalIdentity;
                    options.RequireHttpsMetadata = !environment.IsDevelopment() &&
                        globalSettings.BaseServiceUri.InternalIdentity.StartsWith("https");
                    options.TokenRetriever = TokenRetrieval.FromAuthorizationHeaderOrQueryString();
                    options.NameClaimType = ClaimTypes.Email;
                    options.SupportedTokens = SupportedTokens.Jwt;
                });

            if(addAuthorization != null)
            {
                services.AddAuthorization(config =>
                {
                    addAuthorization.Invoke(config);
                });
            }

            if(environment.IsDevelopment())
            {
                Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            }
        }

        public static IIdentityServerBuilder AddCustomIdentityServerServices(
            this IServiceCollection services, IHostingEnvironment env, GlobalSettings globalSettings)
        {
            var issuerUri = new Uri(globalSettings.BaseServiceUri.InternalIdentity);
            var identityServerBuilder = services
                .AddIdentityServer(options =>
                {
                    options.Endpoints.EnableAuthorizeEndpoint = false;
                    options.Endpoints.EnableIntrospectionEndpoint = false;
                    options.Endpoints.EnableEndSessionEndpoint = false;
                    options.Endpoints.EnableUserInfoEndpoint = false;
                    options.Endpoints.EnableCheckSessionEndpoint = false;
                    options.Endpoints.EnableTokenRevocationEndpoint = false;
                    options.IssuerUri = $"{issuerUri.Scheme}://{issuerUri.Host}";
                    options.Caching.ClientStoreExpiration = new TimeSpan(0, 5, 0);
                })
                .AddInMemoryCaching()
                .AddInMemoryApiResources(ApiResources.GetApiResources())
                .AddClientStoreCache<ClientStore>();

            if(env.IsDevelopment())
            {
                identityServerBuilder.AddDeveloperSigningCredential(false);
            }
            else if(!string.IsNullOrWhiteSpace(globalSettings.IdentityServer.CertificatePassword)
                && File.Exists("identity.pfx"))
            {
                var identityServerCert = CoreHelpers.GetCertificate("identity.pfx",
                    globalSettings.IdentityServer.CertificatePassword);
                identityServerBuilder.AddSigningCredential(identityServerCert);
            }
            else if(!string.IsNullOrWhiteSpace(globalSettings.IdentityServer.CertificateThumbprint))
            {
                var identityServerCert = CoreHelpers.GetCertificate(globalSettings.IdentityServer.CertificateThumbprint);
                identityServerBuilder.AddSigningCredential(identityServerCert);
            }
            else
            {
                throw new Exception("No identity certificate to use.");
            }

            services.AddTransient<ClientStore>();
            services.AddTransient<ICorsPolicyService, AllowAllCorsPolicyService>();
            services.AddScoped<IResourceOwnerPasswordValidator, ResourceOwnerPasswordValidator>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddSingleton<IPersistedGrantStore, PersistedGrantStore>();

            return identityServerBuilder;
        }

        public static void AddCustomDataProtectionServices(
            this IServiceCollection services, IHostingEnvironment env, GlobalSettings globalSettings)
        {
            if(env.IsDevelopment())
            {
                return;
            }

            if(globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.DataProtection.Directory))
            {
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(globalSettings.DataProtection.Directory));
            }

            if(!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Storage.ConnectionString) &&
                CoreHelpers.SettingHasValue(globalSettings.DataProtection.CertificateThumbprint))
            {
                var dataProtectionCert = CoreHelpers.GetCertificate(
                    globalSettings.DataProtection.CertificateThumbprint);
                var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
                services.AddDataProtection()
                    .PersistKeysToAzureBlobStorage(storageAccount, "aspnet-dataprotection/keys.xml")
                    .ProtectKeysWithCertificate(dataProtectionCert);
            }
        }

        public static GlobalSettings AddGlobalSettingsServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            var globalSettings = new GlobalSettings();
            ConfigurationBinder.Bind(configuration.GetSection("GlobalSettings"), globalSettings);
            services.AddSingleton(s => globalSettings);
            return globalSettings;
        }

        public static void UseDefaultMiddleware(this IApplicationBuilder app, IHostingEnvironment env)
        {
            // Add version information to response headers
            app.Use(async (httpContext, next) =>
            {
                httpContext.Response.OnStarting((state) =>
                {
                    httpContext.Response.Headers.Append("Server-Version", CoreHelpers.GetVersion());
                    return Task.FromResult(0);
                }, null);

                await next.Invoke();
            });
        }
    }
}
