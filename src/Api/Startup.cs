using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Identity;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json.Serialization;
using AspNetCoreRateLimit;
using Bit.Api.Middleware;
using Serilog.Events;
using Stripe;
using Bit.Core.Utilities;
using IdentityModel;

namespace Bit.Api
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddSettingsConfiguration(env, "bitwarden-Api");
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
            var globalSettings = services.AddGlobalSettingsServices(Configuration);
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));

            // Data Protection
            services.AddCustomDataProtectionServices(Environment, globalSettings);

            // Stripe Billing
            StripeConfiguration.SetApiKey(globalSettings.StripeApiKey);

            // Repositories
            services.AddSqlServerRepositories();

            // Context
            services.AddScoped<CurrentContext>();

            // Caching
            services.AddMemoryCache();

            // Rate limiting
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            // IdentityServer
            services.AddCustomIdentityServerServices(Environment, globalSettings);

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            services.AddAuthorization(config =>
            {
                config.AddPolicy("Application", policy =>
                {
                    policy.AddAuthenticationSchemes("Bearer2", "Bearer3");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application");
                    policy.RequireClaim(JwtClaimTypes.Scope, "api");
                });
                config.AddPolicy("Web", policy =>
                {
                    policy.AddAuthenticationSchemes("Bearer2", "Bearer3");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application");
                    policy.RequireClaim(JwtClaimTypes.Scope, "api");
                    policy.RequireClaim(JwtClaimTypes.ClientId, "web");
                });
            });

            services.AddScoped<AuthenticatorTokenProvider>();

            // Services
            services.AddBaseServices();
            services.AddDefaultServices();

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
            loggerFactory
                .AddSerilog(env, appLifetime, globalSettings, (e) =>
                {
                    var context = e.Properties["SourceContext"].ToString();
                    if(e.Exception != null && (e.Exception.GetType() == typeof(SecurityTokenValidationException) ||
                        e.Exception.Message == "Bad security stamp."))
                    {
                        return false;
                    }

                    if(context.Contains(typeof(IpRateLimitMiddleware).FullName) && e.Level == LogEventLevel.Information)
                    {
                        return true;
                    }

                    if(context.Contains("IdentityServer4.Validation.TokenRequestValidator"))
                    {
                        return e.Level > LogEventLevel.Error;
                    }

                    return e.Level >= LogEventLevel.Error;
                })
                .AddDebug();

            // Forwarded headers
            if(!env.IsDevelopment())
            {
                app.UseForwardedHeadersForAzure();
            }

            // Rate limiting
            app.UseMiddleware<CustomIpRateLimitMiddleware>();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add Cors
            app.UseCors("All");

            // Add IdentityServer to the request pipeline.
            app.UseIdentityServer();
            app.UseIdentityServerAuthentication(
                GetIdentityOptions(env, IdentityServerAuthority(env, "identity", "33656"), "3"));
            app.UseIdentityServerAuthentication(
                GetIdentityOptions(env, IdentityServerAuthority(env, "api", "4000"), "2"));

            // Add current context
            app.UseMiddleware<CurrentContextMiddleware>();

            // Add MVC to the request pipeline.
            app.UseMvc();
        }

        private IdentityServerAuthenticationOptions GetIdentityOptions(IHostingEnvironment env,
            string authority, string suffix)
        {
            var options = new IdentityServerAuthenticationOptions
            {
                Authority = authority,
                AllowedScopes = new string[] { "api" },
                RequireHttpsMetadata = !env.IsDevelopment(),
                ApiName = "api",
                NameClaimType = ClaimTypes.Email,
                // Suffix until we retire the old jwt schemes.
                AuthenticationScheme = $"Bearer{suffix}",
                TokenRetriever = TokenRetrieval.FromAuthorizationHeaderOrQueryString(
                    $"Bearer{suffix}", $"access_token{suffix}")
            };

            return options;
        }

        private string IdentityServerAuthority(IHostingEnvironment env, string subdomain, string port)
        {
            if(env.IsProduction())
            {
                return $"https://{subdomain}.bitwarden.com";
            }
            else if(env.IsEnvironment("Preview"))
            {
                return $"https://preview-{subdomain}.bitwarden.com";
            }
            else
            {
                return $"http://localhost:{port}";
                //return $"http://192.168.1.3:{port}"; // Desktop external
            }
        }
    }
}
