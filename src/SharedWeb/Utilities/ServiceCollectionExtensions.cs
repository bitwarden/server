// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using AspNetCoreRateLimit;
using Azure.Storage.Queues;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.AdminConsole.Services.NoopImplementations;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.LoginFeatures;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.Services.Implementations;
using Bit.Core.Auth.UserFeatures;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.TrialInitiation;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.HostedServices;
using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Bit.Core.KeyManagement;
using Bit.Core.NotificationCenter;
using Bit.Core.NotificationHub;
using Bit.Core.OrganizationFeatures;
using Bit.Core.Platform;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Repositories;
using Bit.Core.Resources;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.SecretsManager.Repositories.Noop;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Tools.ImportFeatures;
using Bit.Core.Tools.SendFeatures;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault;
using Bit.Core.Vault.Services;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using DnsClient;
using IdentityModel;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Caching.Cosmos;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using NoopRepos = Bit.Core.Repositories.Noop;
using Role = Bit.Core.Entities.Role;
using TableStorageRepos = Bit.Core.Repositories.TableStorage;

namespace Bit.SharedWeb.Utilities;

public static class ServiceCollectionExtensions
{
    public static SupportedDatabaseProviders AddDatabaseRepositories(this IServiceCollection services, GlobalSettings globalSettings)
    {
        var (provider, connectionString) = GetDatabaseProvider(globalSettings);
        services.SetupEntityFramework(connectionString, provider);

        if (provider != SupportedDatabaseProviders.SqlServer)
        {
            services.AddPasswordManagerEFRepositories(globalSettings.SelfHosted);
        }
        else
        {
            services.AddDapperRepositories(globalSettings.SelfHosted);
        }

        if (globalSettings.SelfHosted)
        {
            services.AddSingleton<IInstallationDeviceRepository, NoopRepos.InstallationDeviceRepository>();
        }
        else
        {
            services.AddSingleton<IEventRepository, TableStorageRepos.EventRepository>();
            services.AddSingleton<IInstallationDeviceRepository, TableStorageRepos.InstallationDeviceRepository>();
            services.AddKeyedSingleton<IGrantRepository, Core.Auth.Repositories.Cosmos.GrantRepository>("cosmos");
        }

        return provider;
    }

    public static void AddBaseServices(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<ICipherService, CipherService>();
        services.AddUserServices(globalSettings);
        services.AddTrialInitiationServices();
        services.AddOrganizationServices(globalSettings);
        services.AddPolicyServices();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEmergencyAccessService, EmergencyAccessService>();
        services.AddSingleton<IDeviceService, DeviceService>();
        services.AddScoped<ISsoConfigService, SsoConfigService>();
        services.AddScoped<IAuthRequestService, AuthRequestService>();
        services.AddScoped<IDuoUniversalTokenService, DuoUniversalTokenService>();
        services.AddScoped<ISendAuthorizationService, SendAuthorizationService>();
        services.AddLoginServices();
        services.AddScoped<IOrganizationDomainService, OrganizationDomainService>();
        services.AddVaultServices();
        services.AddReportingServices();
        services.AddKeyManagementServices();
        services.AddNotificationCenterServices();
        services.AddPlatformServices();
        services.AddImportServices();
        services.AddSendServices();
    }

    public static void AddTokenizers(this IServiceCollection services)
    {
        services.AddSingleton<IDataProtectorTokenFactory<OrgDeleteTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<OrgDeleteTokenable>(
                OrgDeleteTokenable.ClearTextPrefix,
                OrgDeleteTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<OrgDeleteTokenable>>>())
        );
        services.AddSingleton<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<EmergencyAccessInviteTokenable>(
                EmergencyAccessInviteTokenable.ClearTextPrefix,
                EmergencyAccessInviteTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<EmergencyAccessInviteTokenable>>>())
        );

        services.AddSingleton<IDataProtectorTokenFactory<SsoTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<SsoTokenable>(
                SsoTokenable.ClearTextPrefix,
                SsoTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<SsoTokenable>>>()));
        services.AddSingleton<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>(
                WebAuthnCredentialCreateOptionsTokenable.ClearTextPrefix,
                WebAuthnCredentialCreateOptionsTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>>()));
        services.AddSingleton<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>(
                WebAuthnLoginAssertionOptionsTokenable.ClearTextPrefix,
                WebAuthnLoginAssertionOptionsTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>>()));
        services.AddSingleton<IDataProtectorTokenFactory<SsoEmail2faSessionTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<SsoEmail2faSessionTokenable>(
                SsoEmail2faSessionTokenable.ClearTextPrefix,
                SsoEmail2faSessionTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<SsoEmail2faSessionTokenable>>>()));

        services.AddSingleton<IOrgUserInviteTokenableFactory, OrgUserInviteTokenableFactory>();
        services.AddSingleton<IDataProtectorTokenFactory<OrgUserInviteTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<OrgUserInviteTokenable>(
                OrgUserInviteTokenable.ClearTextPrefix,
                OrgUserInviteTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<OrgUserInviteTokenable>>>()));
        services.AddSingleton<IDataProtectorTokenFactory<DuoUserStateTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<DuoUserStateTokenable>(
                DuoUserStateTokenable.ClearTextPrefix,
                DuoUserStateTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<DuoUserStateTokenable>>>()));

        services.AddSingleton<IDataProtectorTokenFactory<ProviderDeleteTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<ProviderDeleteTokenable>(
                ProviderDeleteTokenable.ClearTextPrefix,
                ProviderDeleteTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<ProviderDeleteTokenable>>>())
        );
        services.AddSingleton<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>(
            serviceProvider => new DataProtectorTokenFactory<RegistrationEmailVerificationTokenable>(
                RegistrationEmailVerificationTokenable.ClearTextPrefix,
                RegistrationEmailVerificationTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>>()));
        services.AddSingleton<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>(
            serviceProvider => new DataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>(
                TwoFactorAuthenticatorUserVerificationTokenable.ClearTextPrefix,
                TwoFactorAuthenticatorUserVerificationTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>>()));
    }

    public static void AddDefaultServices(this IServiceCollection services, GlobalSettings globalSettings)
    {
        // Required for UserService
        services.AddWebAuthn(globalSettings);
        // Required for HTTP calls
        services.AddHttpClient();

        services.AddSingleton<IStripeAdapter, StripeAdapter>();
        services.AddSingleton<Braintree.IBraintreeGateway>((serviceProvider) =>
        {
            return new Braintree.BraintreeGateway
            {
                Environment = globalSettings.Braintree.Production ?
                    Braintree.Environment.PRODUCTION : Braintree.Environment.SANDBOX,
                MerchantId = globalSettings.Braintree.MerchantId,
                PublicKey = globalSettings.Braintree.PublicKey,
                PrivateKey = globalSettings.Braintree.PrivateKey
            };
        });
        services.AddScoped<IPaymentService, StripePaymentService>();
        services.AddScoped<IPaymentHistoryService, PaymentHistoryService>();
        services.AddScoped<ITwoFactorEmailService, TwoFactorEmailService>();
        services.AddSingleton<IStripeSyncService, StripeSyncService>();
        services.AddSingleton<IMailService, HandlebarsMailService>();
        services.AddSingleton<ILicensingService, LicensingService>();
        services.AddSingleton<ILookupClient>(_ =>
        {
            var options = new LookupClientOptions { Timeout = TimeSpan.FromSeconds(15), UseTcpOnly = true };
            return new LookupClient(options);
        });
        services.AddSingleton<IDnsResolverService, DnsResolverService>();
        services.AddOptionality();
        services.AddTokenizers();

        if (CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName))
        {
            services.AddSingleton<IApplicationCacheService, InMemoryServiceBusApplicationCacheService>();
        }
        else
        {
            services.AddSingleton<IApplicationCacheService, InMemoryApplicationCacheService>();
        }

        var awsConfigured = CoreHelpers.SettingHasValue(globalSettings.Amazon?.AccessKeySecret);
        if (awsConfigured && CoreHelpers.SettingHasValue(globalSettings.Mail?.SendGridApiKey))
        {
            services.AddSingleton<IMailDeliveryService, MultiServiceMailDeliveryService>();
        }
        else if (awsConfigured)
        {
            services.AddSingleton<IMailDeliveryService, AmazonSesMailDeliveryService>();
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.Mail?.Smtp?.Host))
        {
            services.AddSingleton<IMailDeliveryService, MailKitSmtpMailDeliveryService>();
        }
        else
        {
            services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();
        }

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IPushNotificationService, MultiServicePushNotificationService>();
        if (globalSettings.SelfHosted)
        {
            if (globalSettings.Installation.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Installation Id must be set for self-hosted installations.");
            }

            if (CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
                CoreHelpers.SettingHasValue(globalSettings.Installation.Key))
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, RelayPushNotificationService>());
                services.AddSingleton<IPushRegistrationService, RelayPushRegistrationService>();
            }
            else
            {
                services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
            }

            if (CoreHelpers.SettingHasValue(globalSettings.InternalIdentityKey) &&
                CoreHelpers.SettingHasValue(globalSettings.BaseServiceUri.InternalNotifications))
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, NotificationsApiPushNotificationService>());
            }
        }
        else
        {
            services.AddSingleton<INotificationHubPool, NotificationHubPool>();
            services.AddSingleton<IPushRegistrationService, NotificationHubPushRegistrationService>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, NotificationHubPushNotificationService>());
            services.TryAddSingleton<IPushRelayer, NotificationHubPushNotificationService>();
            if (CoreHelpers.SettingHasValue(globalSettings.Notifications?.ConnectionString))
            {
                services.AddKeyedSingleton("notifications",
                    (_, _) => new QueueClient(globalSettings.Notifications.ConnectionString, "notifications"));
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, AzureQueuePushNotificationService>());
            }
        }

        if (!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Mail.ConnectionString))
        {
            services.AddSingleton<IMailEnqueuingService, AzureQueueMailService>();
        }
        else
        {
            services.AddSingleton<IMailEnqueuingService, BlockingMailEnqueuingService>();
        }

        services.AddEventWriteServices(globalSettings);

        if (CoreHelpers.SettingHasValue(globalSettings.Attachment.ConnectionString))
        {
            services.AddSingleton<IAttachmentStorageService, AzureAttachmentStorageService>();
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.Attachment.BaseDirectory))
        {
            services.AddSingleton<IAttachmentStorageService, LocalAttachmentStorageService>();
        }
        else
        {
            services.AddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
        }

        if (CoreHelpers.SettingHasValue(globalSettings.Send.ConnectionString))
        {
            services.AddSingleton<ISendFileStorageService, AzureSendFileStorageService>();
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.Send.BaseDirectory))
        {
            services.AddSingleton<ISendFileStorageService, LocalSendStorageService>();
        }
        else
        {
            services.AddSingleton<ISendFileStorageService, NoopSendFileStorageService>();
        }
    }

    public static void AddOosServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, NoopProviderService>();
        services.AddScoped<IServiceAccountRepository, NoopServiceAccountRepository>();
        services.AddScoped<ISecretRepository, NoopSecretRepository>();
        services.AddScoped<IProjectRepository, NoopProjectRepository>();
    }

    public static void AddNoopServices(this IServiceCollection services)
    {
        services.AddSingleton<IMailService, NoopMailService>();
        services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();
        services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
        services.AddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
        services.AddSingleton<ILicensingService, NoopLicensingService>();
        services.AddSingleton<IEventWriteService, NoopEventWriteService>();
    }

    public static IdentityBuilder AddCustomIdentityServices(
        this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.TryAddTransient(typeof(IOtpTokenProvider<>), typeof(OtpTokenProvider<>));

        services.AddScoped<IOrganizationDuoUniversalTokenProvider, OrganizationDuoUniversalTokenProvider>();
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
                RequiredLength = 12,
                RequireNonAlphanumeric = false,
                RequireUppercase = false
            };
            options.ClaimsIdentity = new ClaimsIdentityOptions
            {
                SecurityStampClaimType = Claims.SecurityStamp,
                UserNameClaimType = JwtClaimTypes.Email,
                UserIdClaimType = JwtClaimTypes.Subject,
            };
            options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
        });

        identityBuilder
            .AddUserStore<UserStore>()
            .AddRoleStore<RoleStore>()
            .AddTokenProvider<DataProtectorTokenProvider<User>>(TokenOptions.DefaultProvider)
            .AddTokenProvider<AuthenticatorTokenProvider>(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Authenticator))
            .AddTokenProvider<EmailTwoFactorTokenProvider>(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Email))
            .AddTokenProvider<YubicoOtpTokenProvider>(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.YubiKey))
            .AddTokenProvider<DuoUniversalTokenProvider>(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Duo))
            .AddTokenProvider<TwoFactorRememberTokenProvider>(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Remember))
            .AddTokenProvider<EmailTokenProvider>(TokenOptions.DefaultEmailProvider)
            .AddTokenProvider<WebAuthnTokenProvider>(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.WebAuthn));

        return identityBuilder;
    }

    public static void AddIdentityAuthenticationServices(
        this IServiceCollection services, GlobalSettings globalSettings, IWebHostEnvironment environment,
        Action<AuthorizationOptions> addAuthorization)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = globalSettings.BaseServiceUri.InternalIdentity;
                options.RequireHttpsMetadata = !environment.IsDevelopment() &&
                    globalSettings.BaseServiceUri.InternalIdentity.StartsWith("https");
                options.TokenValidationParameters.ValidateAudience = false;
                options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
                options.TokenValidationParameters.NameClaimType = ClaimTypes.Email;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = (context) =>
                    {
                        context.Token = TokenRetrieval.FromAuthorizationHeaderOrQueryString()(context.Request);
                        return Task.CompletedTask;
                    }
                };
            });

        if (addAuthorization != null)
        {
            services.AddAuthorization(config =>
            {
                addAuthorization.Invoke(config);
            });
        }

        if (environment.IsDevelopment())
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
        }
    }

    public static void AddCustomDataProtectionServices(
        this IServiceCollection services, IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        var builder = services.AddDataProtection().SetApplicationName("Bitwarden");
        if (env.IsDevelopment())
        {
            return;
        }

        if (globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.DataProtection.Directory))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(globalSettings.DataProtection.Directory));
        }

        if (!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Storage?.ConnectionString))
        {
            X509Certificate2 dataProtectionCert = null;
            if (CoreHelpers.SettingHasValue(globalSettings.DataProtection.CertificateThumbprint))
            {
                dataProtectionCert = CoreHelpers.GetCertificate(
                    globalSettings.DataProtection.CertificateThumbprint);
            }
            else if (CoreHelpers.SettingHasValue(globalSettings.DataProtection.CertificatePassword))
            {
                dataProtectionCert = CoreHelpers.GetBlobCertificateAsync(globalSettings.Storage.ConnectionString, "certificates",
                    "dataprotection.pfx", globalSettings.DataProtection.CertificatePassword)
                    .GetAwaiter().GetResult();
            }
            builder
                .PersistKeysToAzureBlobStorage(globalSettings.Storage.ConnectionString, "aspnet-dataprotection", "keys.xml")
                .ProtectKeysWithCertificate(dataProtectionCert);
        }
    }

    public static IIdentityServerBuilder AddIdentityServerCertificate(
        this IIdentityServerBuilder identityServerBuilder, IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        var certificate = CoreHelpers.GetIdentityServerCertificate(globalSettings);
        if (certificate != null)
        {
            identityServerBuilder.AddSigningCredential(certificate);
        }
        else if (env.IsDevelopment() && !string.IsNullOrEmpty(globalSettings.DevelopmentDirectory))
        {
            var developerSigningKeyPath = Path.Combine(globalSettings.DevelopmentDirectory, "signingkey.jwk");
            identityServerBuilder.AddDeveloperSigningCredential(true, developerSigningKeyPath);
        }
        else if (env.IsDevelopment())
        {
            identityServerBuilder.AddDeveloperSigningCredential(false);
        }
        else
        {
            throw new Exception("No identity certificate to use.");
        }
        return identityServerBuilder;
    }

    public static GlobalSettings AddGlobalSettingsServices(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var globalSettings = new GlobalSettings();
        ConfigurationBinder.Bind(configuration.GetSection("GlobalSettings"), globalSettings);

        if (environment.IsDevelopment() && configuration.GetValue<bool>("developSelfHosted"))
        {
            // Override settings with selfHostedOverride settings
            ConfigurationBinder.Bind(configuration.GetSection("Dev:SelfHostOverride:GlobalSettings"), globalSettings);
        }

        services.AddSingleton(s => globalSettings);
        services.AddSingleton<IGlobalSettings, GlobalSettings>(s => globalSettings);
        return globalSettings;
    }

    public static IServiceCollection AddEventWriteServices(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Events.ConnectionString))
        {
            services.AddKeyedSingleton<IEventWriteService, AzureQueueEventWriteService>("storage");

            if (CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.ConnectionString) &&
                CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.EventTopicName))
            {
                services.AddSingleton<IEventIntegrationPublisher, AzureServiceBusService>();
                services.AddKeyedSingleton<IEventWriteService, EventIntegrationEventWriteService>("broadcast");
            }
            else
            {
                services.AddKeyedSingleton<IEventWriteService, NoopEventWriteService>("broadcast");
            }
        }
        else if (globalSettings.SelfHosted)
        {
            services.AddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("storage");

            if (IsRabbitMqEnabled(globalSettings))
            {
                services.AddSingleton<IEventIntegrationPublisher, RabbitMqService>();
                services.AddKeyedSingleton<IEventWriteService, EventIntegrationEventWriteService>("broadcast");
            }
            else
            {
                services.AddKeyedSingleton<IEventWriteService, NoopEventWriteService>("broadcast");
            }
        }
        else
        {
            services.AddKeyedSingleton<IEventWriteService, NoopEventWriteService>("storage");
            services.AddKeyedSingleton<IEventWriteService, NoopEventWriteService>("broadcast");
        }

        services.AddScoped<IEventWriteService, EventRouteService>();
        return services;
    }

    private static IServiceCollection AddAzureServiceBusEventRepositoryListener(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddSingleton<IEventRepository, TableStorageRepos.EventRepository>();
        services.AddSingleton<AzureTableStorageEventHandler>();
        services.AddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("persistent");
        services.AddSingleton<IHostedService>(provider =>
            new AzureServiceBusEventListenerService(
                handler: provider.GetRequiredService<AzureTableStorageEventHandler>(),
                serviceBusService: provider.GetRequiredService<IAzureServiceBusService>(),
                subscriptionName: globalSettings.EventLogging.AzureServiceBus.EventRepositorySubscriptionName,
                globalSettings: globalSettings,
                logger: provider.GetRequiredService<ILogger<AzureServiceBusEventListenerService>>()
            )
        );

        return services;
    }

    private static IServiceCollection AddAzureServiceBusIntegration<TConfig, THandler>(
        this IServiceCollection services,
        string eventSubscriptionName,
        string integrationSubscriptionName,
        IntegrationType integrationType,
        GlobalSettings globalSettings)
        where TConfig : class
        where THandler : class, IIntegrationHandler<TConfig>
    {
        var routingKey = integrationType.ToRoutingKey();

        services.AddKeyedSingleton<IEventMessageHandler>(routingKey, (provider, _) =>
            new EventIntegrationHandler<TConfig>(
                integrationType,
                provider.GetRequiredService<IEventIntegrationPublisher>(),
                provider.GetRequiredService<IIntegrationFilterService>(),
                provider.GetRequiredService<IOrganizationIntegrationConfigurationRepository>(),
                provider.GetRequiredService<IUserRepository>(),
                provider.GetRequiredService<IOrganizationRepository>(),
                provider.GetRequiredService<ILogger<EventIntegrationHandler<TConfig>>>()));

        services.AddSingleton<IHostedService>(provider =>
            new AzureServiceBusEventListenerService(
                handler: provider.GetRequiredKeyedService<IEventMessageHandler>(routingKey),
                serviceBusService: provider.GetRequiredService<IAzureServiceBusService>(),
                subscriptionName: eventSubscriptionName,
                globalSettings: globalSettings,
                logger: provider.GetRequiredService<ILogger<AzureServiceBusEventListenerService>>()
            )
        );

        services.AddSingleton<IIntegrationHandler<TConfig>, THandler>();
        services.AddSingleton<IHostedService>(provider =>
            new AzureServiceBusIntegrationListenerService(
                handler: provider.GetRequiredService<IIntegrationHandler<TConfig>>(),
                topicName: globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName,
                subscriptionName: integrationSubscriptionName,
                maxRetries: globalSettings.EventLogging.AzureServiceBus.MaxRetries,
                serviceBusService: provider.GetRequiredService<IAzureServiceBusService>(),
                logger: provider.GetRequiredService<ILogger<AzureServiceBusIntegrationListenerService>>()));

        return services;
    }

    public static IServiceCollection AddAzureServiceBusListeners(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (!CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.ConnectionString) ||
            !CoreHelpers.SettingHasValue(globalSettings.EventLogging.AzureServiceBus.EventTopicName))
            return services;

        services.AddSingleton<IIntegrationFilterService, IntegrationFilterService>();
        services.AddSingleton<IAzureServiceBusService, AzureServiceBusService>();
        services.AddSingleton<IEventIntegrationPublisher, AzureServiceBusService>();
        services.AddAzureServiceBusEventRepositoryListener(globalSettings);

        services.AddSlackService(globalSettings);
        services.AddAzureServiceBusIntegration<SlackIntegrationConfigurationDetails, SlackIntegrationHandler>(
            eventSubscriptionName: globalSettings.EventLogging.AzureServiceBus.SlackEventSubscriptionName,
            integrationSubscriptionName: globalSettings.EventLogging.AzureServiceBus.SlackIntegrationSubscriptionName,
            integrationType: IntegrationType.Slack,
            globalSettings: globalSettings);

        services.AddHttpClient(WebhookIntegrationHandler.HttpClientName);
        services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, WebhookIntegrationHandler>(
            eventSubscriptionName: globalSettings.EventLogging.AzureServiceBus.WebhookEventSubscriptionName,
            integrationSubscriptionName: globalSettings.EventLogging.AzureServiceBus.WebhookIntegrationSubscriptionName,
            integrationType: IntegrationType.Webhook,
            globalSettings: globalSettings);

        services.AddAzureServiceBusIntegration<WebhookIntegrationConfigurationDetails, WebhookIntegrationHandler>(
            eventSubscriptionName: globalSettings.EventLogging.AzureServiceBus.HecEventSubscriptionName,
            integrationSubscriptionName: globalSettings.EventLogging.AzureServiceBus.HecIntegrationSubscriptionName,
            integrationType: IntegrationType.Hec,
            globalSettings: globalSettings);

        return services;
    }

    private static IServiceCollection AddRabbitMqEventRepositoryListener(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddSingleton<EventRepositoryHandler>();
        services.AddKeyedSingleton<IEventWriteService, RepositoryEventWriteService>("persistent");

        services.AddSingleton<IHostedService>(provider =>
            new RabbitMqEventListenerService(
                provider.GetRequiredService<EventRepositoryHandler>(),
                globalSettings.EventLogging.RabbitMq.EventRepositoryQueueName,
                provider.GetRequiredService<IRabbitMqService>(),
                provider.GetRequiredService<ILogger<RabbitMqEventListenerService>>()));

        return services;
    }

    private static IServiceCollection AddRabbitMqIntegration<TConfig, THandler>(this IServiceCollection services,
        string eventQueueName,
        string integrationQueueName,
        string integrationRetryQueueName,
        int maxRetries,
        IntegrationType integrationType)
        where TConfig : class
        where THandler : class, IIntegrationHandler<TConfig>
    {
        var routingKey = integrationType.ToRoutingKey();

        services.AddKeyedSingleton<IEventMessageHandler>(routingKey, (provider, _) =>
            new EventIntegrationHandler<TConfig>(
                integrationType,
                provider.GetRequiredService<IEventIntegrationPublisher>(),
                provider.GetRequiredService<IIntegrationFilterService>(),
                provider.GetRequiredService<IOrganizationIntegrationConfigurationRepository>(),
                provider.GetRequiredService<IUserRepository>(),
                provider.GetRequiredService<IOrganizationRepository>(),
                provider.GetRequiredService<ILogger<EventIntegrationHandler<TConfig>>>()));

        services.AddSingleton<IHostedService>(provider =>
            new RabbitMqEventListenerService(
                provider.GetRequiredKeyedService<IEventMessageHandler>(routingKey),
                eventQueueName,
                provider.GetRequiredService<IRabbitMqService>(),
                provider.GetRequiredService<ILogger<RabbitMqEventListenerService>>()));

        services.AddSingleton<IIntegrationHandler<TConfig>, THandler>();
        services.AddSingleton<IHostedService>(provider =>
            new RabbitMqIntegrationListenerService(
                handler: provider.GetRequiredService<IIntegrationHandler<TConfig>>(),
                routingKey: routingKey,
                queueName: integrationQueueName,
                retryQueueName: integrationRetryQueueName,
                maxRetries: maxRetries,
                rabbitMqService: provider.GetRequiredService<IRabbitMqService>(),
                logger: provider.GetRequiredService<ILogger<RabbitMqIntegrationListenerService>>(),
                timeProvider: provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    public static IServiceCollection AddRabbitMqListeners(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (!IsRabbitMqEnabled(globalSettings))
        {
            return services;
        }

        services.AddSingleton<IIntegrationFilterService, IntegrationFilterService>();
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        services.AddSingleton<IEventIntegrationPublisher, RabbitMqService>();
        services.AddRabbitMqEventRepositoryListener(globalSettings);

        services.AddSlackService(globalSettings);
        services.AddRabbitMqIntegration<SlackIntegrationConfigurationDetails, SlackIntegrationHandler>(
            globalSettings.EventLogging.RabbitMq.SlackEventsQueueName,
            globalSettings.EventLogging.RabbitMq.SlackIntegrationQueueName,
            globalSettings.EventLogging.RabbitMq.SlackIntegrationRetryQueueName,
            globalSettings.EventLogging.RabbitMq.MaxRetries,
            IntegrationType.Slack);

        services.AddHttpClient(WebhookIntegrationHandler.HttpClientName);
        services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, WebhookIntegrationHandler>(
            globalSettings.EventLogging.RabbitMq.WebhookEventsQueueName,
            globalSettings.EventLogging.RabbitMq.WebhookIntegrationQueueName,
            globalSettings.EventLogging.RabbitMq.WebhookIntegrationRetryQueueName,
            globalSettings.EventLogging.RabbitMq.MaxRetries,
            IntegrationType.Webhook);

        services.AddRabbitMqIntegration<WebhookIntegrationConfigurationDetails, WebhookIntegrationHandler>(
            globalSettings.EventLogging.RabbitMq.HecEventsQueueName,
            globalSettings.EventLogging.RabbitMq.HecIntegrationQueueName,
            globalSettings.EventLogging.RabbitMq.HecIntegrationRetryQueueName,
            globalSettings.EventLogging.RabbitMq.MaxRetries,
            IntegrationType.Hec);

        return services;
    }

    private static bool IsRabbitMqEnabled(GlobalSettings settings)
    {
        return CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.HostName) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.Username) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.Password) &&
               CoreHelpers.SettingHasValue(settings.EventLogging.RabbitMq.EventExchangeName);
    }

    public static IServiceCollection AddSlackService(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (CoreHelpers.SettingHasValue(globalSettings.Slack.ClientId) &&
            CoreHelpers.SettingHasValue(globalSettings.Slack.ClientSecret) &&
            CoreHelpers.SettingHasValue(globalSettings.Slack.Scopes))
        {
            services.AddHttpClient(SlackService.HttpClientName);
            services.AddSingleton<ISlackService, SlackService>();
        }
        else
        {
            services.AddSingleton<ISlackService, NoopSlackService>();
        }

        return services;
    }

    public static void UseDefaultMiddleware(this IApplicationBuilder app,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();
    }

    public static void UseForwardedHeaders(this IApplicationBuilder app, IGlobalSettings globalSettings)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        if (!globalSettings.UnifiedDeployment)
        {
            // Trust the X-Forwarded-Host header of the nginx docker container
            try
            {
                var nginxIp = Dns.GetHostEntry("nginx")?.AddressList.FirstOrDefault();
                if (nginxIp != null)
                {
                    options.KnownProxies.Add(nginxIp);
                }
            }
            catch
            {
                // Ignore DNS errors
            }
        }

        if (!string.IsNullOrWhiteSpace(globalSettings.KnownProxies))
        {
            var proxies = globalSettings.KnownProxies.Split(',');
            foreach (var proxy in proxies)
            {
                if (IPAddress.TryParse(proxy.Trim(), out var ip))
                {
                    options.KnownProxies.Add(ip);
                }
            }
        }
        if (options.KnownProxies.Count > 1)
        {
            options.ForwardLimit = null;
        }
        app.UseForwardedHeaders(options);
    }

    public static void AddCoreLocalizationServices(this IServiceCollection services)
    {
        services.AddTransient<II18nService, I18nService>();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
    }

    public static IApplicationBuilder UseCoreLocalization(this IApplicationBuilder app)
    {
        var supportedCultures = new[] { "en" };
        return app.UseRequestLocalization(options => options
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures));
    }

    public static IMvcBuilder AddViewAndDataAnnotationLocalization(this IMvcBuilder mvc)
    {
        mvc.Services.AddTransient<IViewLocalizer, I18nViewLocalizer>();
        return mvc.AddViewLocalization(options => options.ResourcesPath = "Resources")
            .AddDataAnnotationsLocalization(options =>
                options.DataAnnotationLocalizerProvider = (type, factory) =>
                {
                    var assemblyName = new AssemblyName(typeof(SharedResources).GetTypeInfo().Assembly.FullName);
                    return factory.Create("SharedResources", assemblyName.Name);
                });
    }

    public static IServiceCollection AddDistributedIdentityServices(this IServiceCollection services)
    {
        services.AddOidcStateDataFormatterCache();
        services.AddSession();
        services.ConfigureApplicationCookie(configure => configure.CookieManager = new DistributedCacheCookieManager());
        services.ConfigureExternalCookie(configure => configure.CookieManager = new DistributedCacheCookieManager());
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, ConfigureOpenIdConnectDistributedOptions>();

        return services;
    }

    public static void AddWebAuthn(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddFido2(options =>
        {
            options.ServerDomain = new Uri(globalSettings.BaseServiceUri.Vault).Host;
            options.ServerName = "Bitwarden";
            options.Origins = new HashSet<string> { globalSettings.BaseServiceUri.Vault, };
            options.TimestampDriftTolerance = 300000;
        });
    }

    /// <summary>
    ///     Adds either an in-memory or distributed IP rate limiter depending if a Redis connection string is available.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="globalSettings"></param>
    public static void AddIpRateLimiting(this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        services.AddHostedService<IpRateLimitSeedStartupService>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        if (!globalSettings.DistributedIpRateLimiting.Enabled ||
            string.IsNullOrEmpty(globalSettings.DistributedIpRateLimiting.RedisConnectionString))
        {
            services.AddInMemoryRateLimiting();
        }
        else
        {
            // Use memory stores for Ip and Client Policy stores as we don't currently use them
            // and they add unnecessary Redis network delays checking for policies that don't exist
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IClientPolicyStore, MemoryCacheClientPolicyStore>();

            // Use a custom Redis processing strategy that skips Ip limiting if Redis is down
            services.AddKeyedSingleton<IConnectionMultiplexer>("rate-limiter", (_, provider) =>
                ConnectionMultiplexer.Connect(globalSettings.DistributedIpRateLimiting.RedisConnectionString));
            services.AddSingleton<IProcessingStrategy, CustomRedisProcessingStrategy>();
        }
    }

    /// <summary>
    ///     Adds an implementation of <see cref="IDistributedCache"/> to the service collection. Uses a memory
    /// cache if self hosted or no Redis connection string is available in GlobalSettings.
    /// </summary>
    public static void AddDistributedCache(
        this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        if (!string.IsNullOrEmpty(globalSettings.DistributedCache?.Redis?.ConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = globalSettings.DistributedCache.Redis.ConnectionString;
            });
        }
        else
        {
            var (databaseProvider, databaseConnectionString) = GetDatabaseProvider(globalSettings);
            if (databaseProvider == SupportedDatabaseProviders.SqlServer)
            {
                services.AddDistributedSqlServerCache(o =>
                {
                    o.ConnectionString = databaseConnectionString;
                    o.SchemaName = "dbo";
                    o.TableName = "Cache";
                });
            }
            else
            {
                services.AddSingleton<IDistributedCache, EntityFrameworkCache>();
            }
        }

        if (!string.IsNullOrEmpty(globalSettings.DistributedCache?.Cosmos?.ConnectionString))
        {
            services.AddKeyedSingleton<IDistributedCache>("persistent", (s, _) =>
                new CosmosCache(new CosmosCacheOptions
                {
                    DatabaseName = "cache",
                    ContainerName = "default",
                    CreateIfNotExists = false,
                    ClientBuilder = new CosmosClientBuilder(globalSettings.DistributedCache?.Cosmos?.ConnectionString)
                }));
        }
        else
        {
            services.AddKeyedSingleton("persistent", (s, _) => s.GetRequiredService<IDistributedCache>());
        }
    }

    public static IServiceCollection AddOptionality(this IServiceCollection services)
    {
        services.AddSingleton<ILdClient>(s =>
        {
            return new LdClient(LaunchDarklyFeatureService.GetConfiguredClient(
                s.GetRequiredService<GlobalSettings>()));
        });

        services.AddScoped<IFeatureService, LaunchDarklyFeatureService>();

        return services;
    }

    private static (SupportedDatabaseProviders provider, string connectionString)
        GetDatabaseProvider(GlobalSettings globalSettings)
    {
        var selectedDatabaseProvider = globalSettings.DatabaseProvider;
        var provider = SupportedDatabaseProviders.SqlServer;
        var connectionString = string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedDatabaseProvider))
        {
            switch (selectedDatabaseProvider.ToLowerInvariant())
            {
                case "postgres":
                case "postgresql":
                    provider = SupportedDatabaseProviders.Postgres;
                    connectionString = globalSettings.PostgreSql.ConnectionString;
                    break;
                case "mysql":
                case "mariadb":
                    provider = SupportedDatabaseProviders.MySql;
                    connectionString = globalSettings.MySql.ConnectionString;
                    break;
                case "sqlite":
                    provider = SupportedDatabaseProviders.Sqlite;
                    connectionString = globalSettings.Sqlite.ConnectionString;
                    break;
                case "sqlserver":
                    connectionString = globalSettings.SqlServer.ConnectionString;
                    break;
                default:
                    break;
            }
        }
        else
        {
            // Default to attempting to use SqlServer connection string if globalSettings.DatabaseProvider has no value.
            connectionString = globalSettings.SqlServer.ConnectionString;
        }

        return (provider, connectionString);
    }
}
