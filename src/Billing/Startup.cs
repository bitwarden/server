using System.Globalization;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.SecretsManager.Repositories.Noop;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stripe;

namespace Bit.Billing;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);
        services.Configure<BillingSettings>(Configuration.GetSection("BillingSettings"));

        // Stripe Billing
        StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
        StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // BitPay Client
        services.AddSingleton<BitPayClient>();

        // PayPal IPN Client
        services.AddHttpClient<IPayPalIPNClient, PayPalIPNClient>();

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();

        //Handlers
        services.AddScoped<IStripeEventUtilityService, StripeEventUtilityService>();
        services.AddScoped<ISubscriptionDeletedHandler, SubscriptionDeletedHandler>();
        services.AddScoped<ISubscriptionUpdatedHandler, SubscriptionUpdatedHandler>();
        services.AddScoped<IUpcomingInvoiceHandler, UpcomingInvoiceHandler>();
        services.AddScoped<IChargeSucceededHandler, ChargeSucceededHandler>();
        services.AddScoped<IChargeRefundedHandler, ChargeRefundedHandler>();
        services.AddScoped<ICustomerUpdatedHandler, CustomerUpdatedHandler>();
        services.AddScoped<IInvoiceCreatedHandler, InvoiceCreatedHandler>();
        services.AddScoped<IPaymentFailedHandler, PaymentFailedHandler>();
        services.AddScoped<IPaymentMethodAttachedHandler, PaymentMethodAttachedHandler>();
        services.AddScoped<IPaymentSucceededHandler, PaymentSucceededHandler>();
        services.AddScoped<IInvoiceFinalizedHandler, InvoiceFinalizedHandler>();
        services.AddScoped<IStripeEventProcessor, StripeEventProcessor>();

        // Identity
        services.AddCustomIdentityServices(globalSettings);
        //services.AddPasswordlessIdentityServices<ReadOnlyDatabaseIdentityUserStore>(globalSettings);

        // Services
        services.AddBaseServices(globalSettings);
        services.AddDefaultServices(globalSettings);
        services.AddDistributedCache(globalSettings);
        services.AddBillingOperations();

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // TODO: Remove when OrganizationUser methods are moved out of OrganizationService, this noop dependency should
        // TODO: no longer be required - see PM-1880
        services.AddScoped<IServiceAccountRepository, NoopServiceAccountRepository>();

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
        });
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        // Authentication
        services.AddAuthentication();

        // Set up HttpClients
        services.AddHttpClient("FreshdeskApi");

        services.AddScoped<IStripeFacade, StripeFacade>();
        services.AddScoped<IStripeEventService, StripeEventService>();
        services.AddScoped<IProviderEventService, ProviderEventService>();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings
    )
    {
        app.UseSerilog(env, appLifetime, globalSettings);

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
