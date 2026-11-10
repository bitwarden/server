// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using AspNetCoreRateLimit;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.AdminConsole.Services.NoopImplementations;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.Services.Implementations;
using Bit.Core.Auth.UserFeatures;
using Bit.Core.Auth.UserFeatures.Devices;
using Bit.Core.Auth.UserFeatures.EmergencyAccess;
using Bit.Core.Auth.UserFeatures.PasswordValidation;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Providers.Services.NoopImplementations;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.TrialInitiation;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.HostedServices;
using Bit.Core.KeyManagement;
using Bit.Core.NotificationCenter;
using Bit.Core.OrganizationFeatures;
using Bit.Core.Pam.Services;
using Bit.Core.Platform;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Enqueuing;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration.Internal;
using Bit.Core.Repositories;
using Bit.Core.Resources;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.SecretsManager.Repositories.Noop;
using Bit.Core.Services;
using Bit.Core.Services.Mail;
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
using Bit.SharedWeb.Play;
using DnsClient;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerGen;
using Constants = Bit.Core.Constants;
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

    /// <summary>
    /// Registers test PlayId tracking services for test data management and cleanup.
    /// This infrastructure is isolated to test environments and enables tracking of test-generated entities.
    /// </summary>
    public static void AddTestPlayIdTracking(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (globalSettings.TestPlayIdTrackingEnabled)
        {
            var (provider, _) = GetDatabaseProvider(globalSettings);

            // Include PlayIdService for tracking Play Ids in repositories
            // We need the http context accessor to use the Singleton version, which pulls from the scoped version
            services.AddHttpContextAccessor();

            services.AddSingleton<IPlayItemService, PlayItemService>();
            services.AddSingleton<IPlayIdService, PlayIdSingletonService>();
            services.AddScoped<PlayIdService>();

            // Replace standard repositories with PlayId tracking decorators
            if (provider == SupportedDatabaseProviders.SqlServer)
            {
                services.AddPlayIdTrackingDapperRepositories();
            }
            else
            {
                services.AddPlayIdTrackingEFRepositories();
            }
        }
        else
        {
            services.AddSingleton<IPlayIdService, NeverPlayIdServices>();
        }
    }

    public static void AddBaseServices(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<ICipherService, CipherService>();
        services.TryAddScoped<ICipherSyncPushService, CipherSyncPushService>();
        services.TryAddScoped<IPartialCipherWriteGuard, PartialCipherWriteGuard>();
        // PAM credential leasing is commercial; OSS builds never gate. The commercial Pam library
        // overrides this default by registering the real gate after AddBaseServices, where the last
        // registration wins — the shape AddOosServices uses for IProviderService. That override must
        // be a plain Add*; a TryAdd* would no-op against this default and leave leasing ungated.
        services.AddScoped<ICipherLeaseGate, UnrestrictedCipherLeaseGate>();
        services.AddUserServices(globalSettings);
        services.AddTrialInitiationServices();
        services.AddOrganizationServices(globalSettings);
        services.AddPolicyServices();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEmergencyAccessService, EmergencyAccessService>();
        services.AddSingleton<IDeviceService, DeviceService>();
        services.AddDeviceServices();
        services.AddScoped<ISsoConfigService, SsoConfigService>();
        services.AddScoped<IAuthRequestService, AuthRequestService>();
        services.AddScoped<IDuoUniversalTokenService, DuoUniversalTokenService>();
        services.AddScoped<ISendAuthorizationService, SendAuthorizationService>();
        services.AddScoped<IOrganizationDomainService, OrganizationDomainService>();
        services.AddVaultServices();
        services.AddReportingServices(globalSettings);
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
        services.AddSingleton<IDataProtectorTokenFactory<IntegrationOAuthStateTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<IntegrationOAuthStateTokenable>(
                IntegrationOAuthStateTokenable.ClearTextPrefix,
                IntegrationOAuthStateTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<IntegrationOAuthStateTokenable>>>()));
        services.AddSingleton<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<TwoFactorRememberTokenable>(
                TwoFactorRememberTokenable.ClearTextPrefix,
                TwoFactorRememberTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<TwoFactorRememberTokenable>>>()));

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
        services.AddSingleton<ITwoFactorUserVerificationTokenableFactory, TwoFactorUserVerificationTokenableFactory>();
        services.AddSingleton<ITwoFactorRememberTokenableFactory, TwoFactorRememberTokenableFactory>();
        services.AddSingleton<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>(
            serviceProvider => new DataProtectorTokenFactory<TwoFactorUserVerificationTokenable>(
                TwoFactorUserVerificationTokenable.ClearTextPrefix,
                TwoFactorUserVerificationTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>>()));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISalesAssistedRegistrationTokenableFactory, SalesAssistedRegistrationTokenableFactory>();
        services.AddSingleton<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(
            serviceProvider => new DataProtectorTokenFactory<SalesAssistedRegistrationTokenable>(
                SalesAssistedRegistrationTokenable.ClearTextPrefix,
                SalesAssistedRegistrationTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>>()));
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
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IPaymentHistoryService, PaymentHistoryService>();
        services.TryAddTransient(typeof(IOtpTokenProvider<>), typeof(OtpTokenProvider<>));
        services.AddScoped<ITwoFactorEmailService, TwoFactorEmailService>();
        services.AddScoped<INewDeviceVerificationOtpStore, NewDeviceVerificationOtpStore>();
        services.AddScoped<IUserVerificationOtpStore, UserVerificationOtpStore>();
        // Legacy mailer service
        services.AddSingleton<IStripeSyncService, StripeSyncService>();
        services.AddSingleton<IMailService, HandlebarsMailService>();
        // Modern mailers
        services.AddMailer();
        services.AddSingleton<ILicensingService, LicensingService>();
        services.AddSingleton<ILookupClient>(_ =>
        {
            var options = new LookupClientOptions { Timeout = TimeSpan.FromSeconds(15), UseTcpOnly = true };
            return new LookupClient(options);
        });
        services.AddSingleton<IDnsResolverService, DnsResolverService>();
        services.ApplyServerCompatibilityLayer();
        services.AddTokenizers();
        services.AddOrganizationAbilityCache(globalSettings);
        services.AddProviderAbilityCache(globalSettings);

        services.ConfigureMailDeliveryOptionsFromGlobalSettings();
        services.AddMailDelivery();

        services.AddPush(globalSettings);
        services.AddPushRegistration();

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

        if (CoreHelpers.SettingHasValue(globalSettings.OrganizationReport.ConnectionString))
        {
            services.AddSingleton<IOrganizationReportStorageService, AzureOrganizationReportStorageService>();
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.OrganizationReport.BaseDirectory))
        {
            services.AddSingleton<IOrganizationReportStorageService, LocalOrganizationReportStorageService>();
        }
        else
        {
            services.AddSingleton<IOrganizationReportStorageService, NoopOrganizationReportStorageService>();
        }
    }

    public static void AddOosServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, NoopProviderService>();
        services.AddScoped<IServiceAccountRepository, NoopServiceAccountRepository>();
        services.AddScoped<ISecretRepository, NoopSecretRepository>();
        services.AddScoped<ISecretVersionRepository, NoopSecretVersionRepository>();
        services.AddScoped<IProjectRepository, NoopProjectRepository>();
        services.AddTransient<IBusinessUnitConverter, NoopBusinessUnitConverter>();
    }

    public static void AddNoopServices(this IServiceCollection services)
    {
        services.AddSingleton<IMailService, NoopMailService>();
        services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();
        services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
        services.AddSingleton<IAttachmentStorageService, NoopAttachmentStorageService>();
        services.AddSingleton<ILicensingService, NoopLicensingService>();
    }

    public static IdentityBuilder AddCustomIdentityServices(
        this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddScoped<IOrganizationDuoUniversalTokenProvider, OrganizationDuoUniversalTokenProvider>();
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = PasswordValidationConstants.PasswordHasherKdfIterations);
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
    }



    public static IIdentityServerBuilder AddIdentityServerCertificate(
        this IIdentityServerBuilder identityServerBuilder, IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        return AddSigningCertificateSet(identityServerBuilder, env, globalSettings,
            GetIdentitySigningCertificates(new IdentityCertificates(globalSettings)),
            CertificateSourceReport.IdentitySigning);
    }

    /// <summary>
    /// Registers the loaded SSO signing certificates: the active certificate signs, and the staged and validation
    /// certificates are published for token validation. With no active certificate, a Development host uses a
    /// developer signing key.
    /// </summary>
    /// <exception cref="Exception">The host is not a Development host and has no active certificate.</exception>
    public static IIdentityServerBuilder AddSsoSigningCertificate(
        this IIdentityServerBuilder identityServerBuilder, IWebHostEnvironment env, GlobalSettings globalSettings,
        SigningCertificateSet certificates)
    {
        return AddSigningCertificateSet(identityServerBuilder, env, globalSettings, certificates,
            CertificateSourceReport.SsoSigning);
    }

    /// <summary>
    /// Gets the SSO signing certificates. An SSO active source uses only the SSO slots. With no SSO active source,
    /// a self-hosted or Development host uses the Identity signing certificates from <paramref name="identity"/> and
    /// the SSO slots. A cloud host that is not a Development host must have an SSO active source.
    /// </summary>
    /// <exception cref="InvalidOperationException">A cloud host has no SSO active source, or a configured
    /// certificate did not load, or a configured certificate does not have an RSA key.</exception>
    public static SigningCertificateSet GetSsoSigningCertificates(IWebHostEnvironment env,
        GlobalSettings globalSettings, IdentityCertificates identity)
    {
        const string ssoServerPath = "globalSettings:ssoServer";
        var ssoServer = globalSettings.SsoServer;
        var sso = SsoCertificateSlots.Load(env, globalSettings, ssoServer, ssoServer.FutureCertificate,
            ssoServer.ValidationCertificates, ssoServerPath, $"{ssoServerPath}:validationCertificates", "SSO signing");
        if (!sso.IsFallback)
        {
            return new SigningCertificateSet(sso.Active, sso.Staged, sso.Retired, IsFallback: false);
        }

        return new SigningCertificateSet(identity.Active, [.. identity.Staged, .. sso.Staged],
            [.. identity.Validation, .. sso.Retired], IsFallback: true);
    }

    /// <summary>
    /// Logs one entry for each certificate role: the role, the source, and the thumbprint of each active, staged,
    /// and retired certificate. Logs one warning for each role whose root has a password but no location,
    /// thumbprint, or blob name. Logs one warning for each role whose future slot is active but not enabled.
    /// The log names only setting paths, roles, sources, and thumbprints.
    /// </summary>
    public static void LogCertificateSources(ILogger logger, IEnumerable<CertificateSourceReport> reports,
        GlobalSettings globalSettings)
    {
        foreach (var report in reports)
        {
            var (source, root, future, settingPath) = GetRoleSettings(report.Role, globalSettings);
            logger.LogInformation(Constants.BypassFiltersEventId,
                "{Role} certificate source: {Source}. Active thumbprint: {ActiveThumbprint}. " +
                "Staged thumbprints: {StagedThumbprints}. " +
                "Retired (validation or decryption) thumbprints: {RetiredThumbprints}.",
                report.Role, report.IsFallback ? "Identity fallback" : source,
                report.Active?.Thumbprint ?? "none", Thumbprints(report.Staged), Thumbprints(report.Retired));

            if (CoreHelpers.SettingHasValue(root.CertificatePassword) &&
                !CoreHelpers.SettingHasValue(root.CertificateLocation) &&
                !CoreHelpers.SettingHasValue(root.CertificateThumbprint) &&
                !CoreHelpers.SettingHasValue(root.BlobName))
            {
                logger.LogWarning(Constants.BypassFiltersEventId,
                    "{SettingPath} has a certificate password, but no certificate location, thumbprint, or blob " +
                    "name. The {Role} role does not load a certificate from this section.",
                    settingPath, report.Role);
            }

            if (future.Active && !future.Enabled)
            {
                logger.LogWarning(Constants.BypassFiltersEventId,
                    "{SettingPath}:futureCertificate has active set to true and enabled set to false. " +
                    "The host ignores this slot. No promotion happened for the {Role} role.",
                    settingPath, report.Role);
            }
        }
    }

    private static string Thumbprints(IReadOnlyList<X509Certificate2> certificates) =>
        certificates.Count == 0 ? "none" : string.Join(", ", certificates.Select(certificate => certificate.Thumbprint));

    private static (string Source, ICertificateSettings Root, GlobalSettings.FutureCertificateSettings Future,
        string SettingPath) GetRoleSettings(string role, GlobalSettings globalSettings) => role switch
        {
            CertificateSourceReport.IdentitySigning => ("IdentityServer", globalSettings.IdentityServer,
                globalSettings.IdentityServer.FutureCertificate, "globalSettings:identityServer"),
            CertificateSourceReport.SsoSigning => ("SsoServer", globalSettings.SsoServer,
                globalSettings.SsoServer.FutureCertificate, "globalSettings:ssoServer"),
            CertificateSourceReport.SsoEncryption => ("SsoServer", globalSettings.SsoServer.Encryption,
                globalSettings.SsoServer.Encryption.FutureCertificate, "globalSettings:ssoServer:encryption"),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The certificate role is not known."),
        };

    private static IIdentityServerBuilder AddSigningCertificateSet(IIdentityServerBuilder identityServerBuilder,
        IWebHostEnvironment env, GlobalSettings globalSettings, SigningCertificateSet certificates, string role)
    {
        identityServerBuilder.Services.AddSingleton(new CertificateSourceReport(role, certificates.Active,
            certificates.Staged, certificates.Validation, certificates.IsFallback));

        if (certificates.Active != null)
        {
            AddSigningCertificates(identityServerBuilder, certificates.Active,
                certificates.Staged.Concat(certificates.Validation));
        }
        else
        {
            AddDeveloperSigningCredential(identityServerBuilder, env, globalSettings);
        }
        return identityServerBuilder;
    }

    private static void AddDeveloperSigningCredential(IIdentityServerBuilder identityServerBuilder,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        if (env.IsDevelopment() && !string.IsNullOrEmpty(globalSettings.DevelopmentDirectory))
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
    }

    private static SigningCertificateSet GetIdentitySigningCertificates(IdentityCertificates identity) =>
        new(identity.Active, identity.Staged, identity.Validation, IsFallback: false);

    private static IIdentityServerBuilder AddSigningCertificates(IIdentityServerBuilder identityServerBuilder,
        X509Certificate2 active, IEnumerable<X509Certificate2> published)
    {
        identityServerBuilder.AddSigningCredential(active);

        var validationKeys = published
            .DistinctBy(certificate => certificate.Thumbprint)
            .Where(certificate => certificate.Thumbprint != active.Thumbprint)
            .Select(certificate => new SecurityKeyInfo
            {
                Key = new X509SecurityKey(certificate) { KeyId = certificate.Thumbprint + SecurityAlgorithms.RsaSha256 },
                SigningAlgorithm = SecurityAlgorithms.RsaSha256,
            })
            .ToArray();
        if (validationKeys.Length > 0)
        {
            identityServerBuilder.AddValidationKey(validationKeys);
        }
        return identityServerBuilder;
    }

    public static void UseDefaultMiddleware(this IApplicationBuilder app,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();
        if (globalSettings.TestPlayIdTrackingEnabled)
        {
            app.UseMiddleware<PlayIdMiddleware>();
        }
    }

    public static void UseForwardedHeaders(this IApplicationBuilder app, IGlobalSettings globalSettings)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        if (!globalSettings.LiteDeployment)
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

        if (!string.IsNullOrWhiteSpace(globalSettings.KnownNetworks))
        {
            var proxyNetworks = globalSettings.KnownNetworks.Split(',');
            foreach (var proxyNetwork in proxyNetworks)
            {
                if (System.Net.IPNetwork.TryParse(proxyNetwork.Trim(), out var ipn))
                {
                    options.KnownIPNetworks.Add(ipn);
                }
            }
        }

        if (options.KnownProxies.Count > 1 || options.KnownIPNetworks.Count > 1)
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
            options.TimestampDriftTolerance = 300000;

            if (globalSettings.Fido2?.Origins?.Any() == true)
            {
                options.Origins = new HashSet<string>(globalSettings.Fido2.Origins);
            }
            else
            {
                // Default to allowing the vault domain and chromium browser extension IDs
                options.Origins = new HashSet<string> {
                    globalSettings.BaseServiceUri.Vault,
                    Constants.BrowserExtensions.ChromeId,
                    Constants.BrowserExtensions.ChromeBetaId,
                    Constants.BrowserExtensions.EdgeId,
                    Constants.BrowserExtensions.OperaId
                 };
            }
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
    ///     Adds an implementation of <see cref="IDistributedCache"/> to the service collection. Uses Redis
    /// if a connection string is available in GlobalSettings, a database-backed distributed cache if
    /// self-hosted or a distributed memory cache as a final fallback.
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
            if (globalSettings.SelfHosted)
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
            else
            {
                services.AddDistributedMemoryCache();
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

    /// <summary>
    /// Adds a server with its corresponding OAuth2 client credentials security definition and requirement.
    /// </summary>
    /// <param name="config">The SwaggerGen configuration</param>
    /// <param name="serverId">Unique identifier for this server (e.g., "us-server", "eu-server")</param>
    /// <param name="serverUrl">The API server URL</param>
    /// <param name="identityTokenUrl">The identity server token URL</param>
    /// <param name="serverDescription">Human-readable description for the server</param>
    public static void AddSwaggerServerWithSecurity(
        this SwaggerGenOptions config,
        string serverId,
        string serverUrl,
        string identityTokenUrl,
        string serverDescription)
    {
        // Add server
        config.AddServer(new OpenApiServer
        {
            Url = serverUrl,
            Description = serverDescription
        });

        // Add security definition
        config.AddSecurityDefinition(serverId, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = $"**Use this option if you've selected the {serverDescription}**",
            Flows = new OpenApiOAuthFlows
            {
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri(identityTokenUrl),
                    Scopes = new Dictionary<string, string>
                    {
                        { ApiScopes.ApiOrganization, $"Organization APIs ({serverDescription})" },
                    },
                }
            },
        });

        // Add security requirement
        config.AddSecurityRequirement((document) => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(serverId, document)] = [ApiScopes.ApiOrganization]
        });
    }
}
