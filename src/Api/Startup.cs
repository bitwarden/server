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
using Bit.Core.Domains;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Services;
using SqlServerRepos = Bit.Core.Repositories.SqlServer;
using System.Text;
using Loggr.Extensions.Logging;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Serialization;
using AspNetCoreRateLimit;
using Bit.Api.Middleware;
using IdentityServer4.Validation;
using IdentityServer4.Services;
using IdentityModel.AspNetCore.OAuth2Introspection;
using IdentityServer4.Stores;
using Bit.Core.Utilities;

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
                builder.AddUserSecrets();
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry(Configuration);

            var provider = services.BuildServiceProvider();

            // Options
            services.AddOptions();

            // Settings
            var globalSettings = new GlobalSettings();
            ConfigurationBinder.Bind(Configuration.GetSection("GlobalSettings"), globalSettings);
            services.AddSingleton(s => globalSettings);
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));

            // Repositories
            services.AddSingleton<IUserRepository, SqlServerRepos.UserRepository>();
            services.AddSingleton<ICipherRepository, SqlServerRepos.CipherRepository>();
            services.AddSingleton<IDeviceRepository, SqlServerRepos.DeviceRepository>();
            services.AddSingleton<IGrantRepository, SqlServerRepos.GrantRepository>();

            // Context
            services.AddScoped<CurrentContext>();

            // Caching
            services.AddMemoryCache();

            // Rate limiting
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            // IdentityServer
            var identityServerCert = CoreHelpers.GetCertificate(globalSettings.IdentityServer.CertificateThumbprint);
            services.AddIdentityServer()
                .AddSigningCredential(identityServerCert)
                .AddInMemoryApiResources(ApiResources.GetApiResources())
                .AddInMemoryClients(Clients.GetClients());
            services.AddSingleton<IResourceOwnerPasswordValidator, ResourceOwnerPasswordValidator>();
            services.AddSingleton<IProfileService, ProfileService>();
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
            .AddTokenProvider<AuthenticatorTokenProvider>("Authenticator")
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
            }).AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver()); ;
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            GlobalSettings globalSettings)
        {
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();

            if(!env.IsDevelopment())
            {
                loggerFactory.AddLoggr(
                    (category, logLevel, eventId) =>
                    {
                        // Bad security stamp exception
                        if(category == typeof(JwtBearerMiddleware).FullName && eventId.Id == 3 && logLevel == LogLevel.Error)
                        {
                            return false;
                        }

                        // IP blocks
                        if(category == typeof(IpRateLimitMiddleware).FullName && logLevel >= LogLevel.Information)
                        {
                            return true;
                        }

                        return logLevel >= LogLevel.Error;
                    },
                    globalSettings.Loggr.LogKey,
                    globalSettings.Loggr.ApiKey);
            }

            // Rate limiting
            app.UseMiddleware<CustomIpRateLimitMiddleware>();

            // Insights
            app.UseApplicationInsightsRequestTelemetry();
            app.UseApplicationInsightsExceptionTelemetry();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add Cors
            app.UseCors("All");

            // Add IdentityServer to the request pipeline.
            app.UseIdentityServer();
            app.UseIdentityServerAuthentication(new IdentityServerAuthenticationOptions
            {
                AllowedScopes = new string[] { "api" },
                Authority = env.IsProduction() ? "https://api.bitwarden.com" : "http://localhost:4000",
                RequireHttpsMetadata = env.IsProduction(),
                ApiName = "Vault API",
                AuthenticationScheme = "Bearer2",
                TokenRetriever = TokenRetrieval.FromAuthorizationHeader("Bearer2")
            });

            // Add Jwt authentication to the request pipeline.
            app.UseJwtBearerIdentity();

            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
