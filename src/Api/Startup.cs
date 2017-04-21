using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Models.Table;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Services;
using SqlServerRepos = Bit.Core.Repositories.SqlServer;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Serialization;
using AspNetCoreRateLimit;
using Bit.Api.Middleware;
using IdentityServer4.Validation;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Bit.Core.Utilities;
using Serilog;
using Serilog.Events;
using Bit.Api.IdentityServer;
using Bit.Core.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.WindowsAzure.Storage;
using Stripe;

namespace Bit.Api
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("settings.json")
                .AddJsonFile($"settings.{env.EnvironmentName}.json", optional: true);

            if(env.IsDevelopment())
            {
                builder.AddUserSecrets<Startup>();
            }

            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
            Environment = env;
        }

        public IConfigurationRoot Configuration { get; private set; }
        public IHostingEnvironment Environment { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            var provider = services.BuildServiceProvider();

            // Options
            services.AddOptions();

            // Settings
            var globalSettings = new GlobalSettings();
            ConfigurationBinder.Bind(Configuration.GetSection("GlobalSettings"), globalSettings);
            services.AddSingleton(s => globalSettings);
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));

            // Data Protection
            if(Environment.IsProduction())
            {
                var dataProtectionCert = CoreHelpers.GetCertificate(globalSettings.DataProtection.CertificateThumbprint);
                var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
                services.AddDataProtection()
                    .PersistKeysToAzureBlobStorage(storageAccount, "aspnet-dataprotection/keys.xml")
                    .ProtectKeysWithCertificate(dataProtectionCert);
            }

            // Stripe Billing
            StripeConfiguration.SetApiKey(globalSettings.StripeApiKey);

            // Repositories
            services.AddSingleton<IUserRepository, SqlServerRepos.UserRepository>();
            services.AddSingleton<ICipherRepository, SqlServerRepos.CipherRepository>();
            services.AddSingleton<IDeviceRepository, SqlServerRepos.DeviceRepository>();
            services.AddSingleton<IGrantRepository, SqlServerRepos.GrantRepository>();
            services.AddSingleton<IOrganizationRepository, SqlServerRepos.OrganizationRepository>();
            services.AddSingleton<IOrganizationUserRepository, SqlServerRepos.OrganizationUserRepository>();
            services.AddSingleton<ISubvaultRepository, SqlServerRepos.SubvaultRepository>();
            services.AddSingleton<ISubvaultUserRepository, SqlServerRepos.SubvaultUserRepository>();
            services.AddSingleton<IFolderRepository, SqlServerRepos.FolderRepository>();
            services.AddSingleton<ISubvaultCipherRepository, SqlServerRepos.SubvaultCipherRepository>();

            // Context
            services.AddScoped<CurrentContext>();

            // Caching
            services.AddMemoryCache();

            // Rate limiting
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            // IdentityServer
            var identityServerBuilder = services.AddIdentityServer(options =>
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

            if(Environment.IsProduction())
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

            // Identity
            services.AddTransient<ILookupNormalizer, LowerInvariantLookupNormalizer>();
            services.AddJwtBearerIdentity(options =>
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
            })
            .AddUserStore<UserStore>()
            .AddRoleStore<RoleStore>()
            .AddTokenProvider<AuthenticatorTokenProvider>(TwoFactorProviderType.Authenticator.ToString())
            .AddTokenProvider<EmailTokenProvider<User>>(TokenOptions.DefaultEmailProvider);

            var jwtIdentityOptions = provider.GetRequiredService<IOptions<JwtBearerIdentityOptions>>().Value;
            services.AddAuthorization(config =>
            {
                config.AddPolicy("Application", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "Bearer2");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimTypes.AuthenticationMethod, jwtIdentityOptions.AuthenticationMethod);
                });

                config.AddPolicy("TwoFactor", policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "Bearer2");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimTypes.AuthenticationMethod, jwtIdentityOptions.TwoFactorAuthenticationMethod);
                });
            });

            services.AddScoped<AuthenticatorTokenProvider>();

            // Services
            services.AddSingleton<IMailService, SendGridMailService>();
            services.AddSingleton<ICipherService, CipherService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPushService, PushSharpPushService>();
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IBlockIpService, AzureQueueBlockIpService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<ISubvaultService, SubvaultService>();

            // Cors
            services.AddCors(config =>
            {
                config.AddPolicy("All", policy =>
                    policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().SetPreflightMaxAge(TimeSpan.FromDays(1)));
            });

            // MVC
            services.AddMvc(config =>
            {
                config.Filters.Add(new ExceptionHandlerFilterAttribute());
                config.Filters.Add(new ModelStateValidationFilterAttribute());

                // Allow JSON of content type "text/plain" to avoid cors preflight
                var textPlainMediaType = MediaTypeHeaderValue.Parse("text/plain");
                foreach(var jsonFormatter in config.InputFormatters.OfType<JsonInputFormatter>())
                {
                    jsonFormatter.SupportedMediaTypes.Add(textPlainMediaType);
                }
            }).AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            if(env.IsProduction())
            {
                Func<LogEvent, bool> serilogFilter = (e) =>
                {
                    var context = e.Properties["SourceContext"].ToString();
                    if(e.Exception != null && e.Exception.GetType() == typeof(SecurityTokenValidationException) || e.Exception.Message == "Bad security stamp.")
                    {
                        return false;
                    }

                    if(context == typeof(IpRateLimitMiddleware).FullName && e.Level == LogEventLevel.Information)
                    {
                        return true;
                    }

                    return e.Level >= LogEventLevel.Error;
                };

                var serilog = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Filter.ByIncludingOnly(serilogFilter)
                    .WriteTo.AzureDocumentDB(new Uri(globalSettings.DocumentDb.Uri), globalSettings.DocumentDb.Key,
                        timeToLive: TimeSpan.FromDays(7))
                    .CreateLogger();

                loggerFactory.AddSerilog(serilog);
                appLifetime.ApplicationStopped.Register(Log.CloseAndFlush);
            }

            loggerFactory.AddDebug();

            // Rate limiting
            app.UseMiddleware<CustomIpRateLimitMiddleware>();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add Cors
            app.UseCors("All");

            // Add IdentityServer to the request pipeline.
            app.UseIdentityServer();
            app.UseIdentityServerAuthentication(GetIdentityOptions(env));

            // Add Jwt authentication to the request pipeline.
            app.UseJwtBearerIdentity();

            // Add current context
            app.UseMiddleware<CurrentContextMiddleware>();

            // Add MVC to the request pipeline.
            app.UseMvc();
        }

        private IdentityServerAuthenticationOptions GetIdentityOptions(IHostingEnvironment env)
        {
            var options = new IdentityServerAuthenticationOptions
            {
                AllowedScopes = new string[] { "api" },
                RequireHttpsMetadata = env.IsProduction(),
                ApiName = "api",
                NameClaimType = ClaimTypes.Email,
                // Version "2" until we retire the old jwt scheme and replace it with this one.
                AuthenticationScheme = "Bearer2",
                TokenRetriever = TokenRetrieval.FromAuthorizationHeaderOrQueryString("Bearer2", "access_token2")
            };

            if(env.IsProduction())
            {
                options.Authority = "https://api.bitwarden.com";
            }
            else if(env.IsEnvironment("Preview"))
            {
                options.Authority = "https://preview-api.bitwarden.com";
            }
            else
            {
                options.Authority = "http://localhost:4000";
                //options.Authority = "http://169.254.80.80:4000"; // for VS Android Emulator
                //options.Authority = "http://192.168.1.8:4000"; // Desktop external
            }

            return options;
        }
    }
}
