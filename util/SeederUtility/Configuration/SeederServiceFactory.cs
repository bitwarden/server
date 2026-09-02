using AutoMapper;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.SeederUtility.Configuration;

internal static class SeederServiceFactory
{
    internal static SeederServiceScope Create(SeederServiceOptions options)
    {
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, enableMangling: options.EnableMangling);
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return new SeederServiceScope(provider, scope);
    }
}

internal record SeederServiceOptions
{
    internal required bool EnableMangling { get; init; }
}

internal sealed class SeederServiceScope : IDisposable
{
    internal DatabaseContext Db { get; }

    internal IMapper Mapper { get; }

    internal IPasswordHasher<User> PasswordHasher { get; }

    internal IManglerService Mangler { get; }

    internal ILicensingService LicensingService { get; }

    internal IAttachmentStorageService AttachmentStorageService { get; }

    internal Func<IStripeBillingInitializer> BillingInitializer { get; }

    internal ISeederLicenseSigner LicenseSigner { get; }

    internal ILoggerFactory LoggerFactory { get; }

    internal SeederDependencies ToDependencies()
        => new(Db, Mapper, PasswordHasher, Mangler, LicensingService, AttachmentStorageService, LicenseSigner, LoggerFactory)
        {
            BillingInitializer = BillingInitializer,
        };

    private readonly ServiceProvider _provider;

    private readonly IServiceScope _scope;

    internal SeederServiceScope(ServiceProvider provider, IServiceScope scope)
    {
        _provider = provider;
        _scope = scope;
        var sp = scope.ServiceProvider;
        Db = sp.GetRequiredService<DatabaseContext>();
        Mapper = sp.GetRequiredService<IMapper>();
        PasswordHasher = sp.GetRequiredService<IPasswordHasher<User>>();
        Mangler = sp.GetRequiredService<IManglerService>();
        LicensingService = sp.GetRequiredService<ILicensingService>();
        AttachmentStorageService = sp.GetRequiredService<IAttachmentStorageService>();
        // Deferred so the billing DI graph (IOrganizationBillingService -> IBraintreeGateway, IStripeAdapter,
        // ISubscriberService -> IPriceIncreaseScheduler -> IFeatureService) is only constructed by commands
        // that actually opt into Stripe billing, not on every command. Closes over the scope, not the root
        // provider: the billing graph is scoped and transient throughout, and capturing it on the root
        // provider would outlive the DbContext it depends on.
        BillingInitializer = () => sp.GetRequiredService<IStripeBillingInitializer>();
        LicenseSigner = sp.GetRequiredService<ISeederLicenseSigner>();
        LoggerFactory = sp.GetRequiredService<ILoggerFactory>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
