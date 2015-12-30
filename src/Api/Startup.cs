using System;
using System.Security.Claims;
using Microsoft.AspNet.Authentication.JwtBearer;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.OptionsModel;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Domains;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.Repositories.DocumentDB.Utilities;
using Bit.Core.Services;
using Repos = Bit.Core.Repositories.DocumentDB;

namespace Bit.Api
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .AddJsonFile($"settings.{env.EnvironmentName}.json", optional: true);

            if(env.IsDevelopment())
            {
                builder.AddUserSecrets();
            }

            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<GlobalSettings>(Configuration.GetSection("globalSettings"));

            // Options
            services.AddOptions();

            // Settings
            var provider = services.BuildServiceProvider();
            var globalSettings = provider.GetRequiredService<IOptions<GlobalSettings>>().Value;
            services.AddSingleton(s => globalSettings);

            // Repositories
            var documentDBClient = DocumentDBHelpers.InitClient(globalSettings.DocumentDB);
            services.AddSingleton<IUserRepository>(s => new Repos.UserRepository(documentDBClient, globalSettings.DocumentDB.DatabaseId));
            services.AddSingleton<ISiteRepository>(s => new Repos.SiteRepository(documentDBClient, globalSettings.DocumentDB.DatabaseId));
            services.AddSingleton<IFolderRepository>(s => new Repos.FolderRepository(documentDBClient, globalSettings.DocumentDB.DatabaseId));
            services.AddSingleton<ICipherRepository>(s => new Repos.CipherRepository(documentDBClient, globalSettings.DocumentDB.DatabaseId));

            // Context
            services.AddScoped<CurrentContext>();

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
                    RequireNonLetterOrDigit = false,
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
                // TODO: Symmetric key
                // waiting on https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/250
                jwtBearerOptions.SigningCredentials = null;
            })
            .AddUserStore<UserStore>()
            .AddRoleStore<RoleStore>()
            .AddTokenProvider<AuthenticatorTokenProvider>("Authenticator")
            .AddTokenProvider<EmailTokenProvider<User>>(TokenOptions.DefaultEmailProvider);

            var jwtIdentityOptions = provider.GetRequiredService<IOptions<JwtBearerIdentityOptions>>().Value;
            services.AddAuthorization(config =>
            {
                config.AddPolicy("Application", new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme‌​)
                    .RequireAuthenticatedUser().RequireClaim(ClaimTypes.AuthenticationMethod, jwtIdentityOptions.AuthenticationMethod).Build());

                config.AddPolicy("TwoFactor", new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser().RequireClaim(ClaimTypes.AuthenticationMethod, jwtIdentityOptions.TwoFactorAuthenticationMethod).Build());
            });

            services.AddScoped<AuthenticatorTokenProvider>();

            // Services
            services.AddSingleton<IMailService, MailService>();
            services.AddSingleton<ICipherService, CipherService>();
            services.AddScoped<IUserService, UserService>();

            // Cors
            services.AddCors(config =>
            {
                config.AddPolicy("All", policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
            });

            // MVC
            services.AddMvc(config =>
            {
                config.Filters.Add(new ExceptionHandlerFilterAttribute());
                config.Filters.Add(new ModelStateValidationFilterAttribute());
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.MinimumLevel = LogLevel.Information;
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();

            // Add the platform handler to the request pipeline.
            app.UseIISPlatformHandler();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add Cors
            app.UseCors("All");

            // Add Jwt authentication to the request pipeline.
            app.UseJwtBearerIdentity();

            // Add MVC to the request pipeline.
            app.UseMvc();
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
