using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AutoMapper;
using Bit.Core.Settings;
using Bit.Infrastructure.EFIntegration.Test.Helpers;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Tools.Models;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class ServiceScopeFactoryBuilder : ISpecimenBuilder
{
    private DbContextOptions<DatabaseContext> _options { get; set; }
    public ServiceScopeFactoryBuilder(DbContextOptions<DatabaseContext> options)
    {
        _options = options;
    }

    public object Create(object request, ISpecimenContext context)
    {
        var fixture = new Fixture();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var dbContext = new DatabaseContext(_options);
        serviceProvider.GetService(typeof(DatabaseContext)).Returns(dbContext);

        var serviceScope = Substitute.For<IServiceScope>();
        serviceScope.ServiceProvider.Returns(serviceProvider);

        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        return serviceScopeFactory;
    }
}

public class EfRepositoryListBuilder<T> : ISpecimenBuilder where T : BaseEntityFrameworkRepository
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var t = request as ParameterInfo;
        if (t == null || t.ParameterType != typeof(List<T>))
        {
            return new NoSpecimen();
        }

        var list = new List<T>();
        foreach (var option in DatabaseOptionsFactory.Options)
        {
            var fixture = new Fixture();
            fixture.Customize<IServiceScopeFactory>(x => x.FromFactory(new ServiceScopeFactoryBuilder(option)));
            fixture.Customize<IMapper>(x => x.FromFactory(() =>
                new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<AuthRequestMapperProfile>();
                    cfg.AddProfile<CipherMapperProfile>();
                    cfg.AddProfile<CollectionCipherMapperProfile>();
                    cfg.AddProfile<CollectionMapperProfile>();
                    cfg.AddProfile<DeviceMapperProfile>();
                    cfg.AddProfile<EmergencyAccessMapperProfile>();
                    cfg.AddProfile<EventMapperProfile>();
                    cfg.AddProfile<FolderMapperProfile>();
                    cfg.AddProfile<GrantMapperProfile>();
                    cfg.AddProfile<GroupMapperProfile>();
                    cfg.AddProfile<GroupUserMapperProfile>();
                    cfg.AddProfile<InstallationMapperProfile>();
                    cfg.AddProfile<OrganizationMapperProfile>();
                    cfg.AddProfile<OrganizationSponsorshipMapperProfile>();
                    cfg.AddProfile<OrganizationUserMapperProfile>();
                    cfg.AddProfile<ProviderMapperProfile>();
                    cfg.AddProfile<ProviderUserMapperProfile>();
                    cfg.AddProfile<ProviderOrganizationMapperProfile>();
                    cfg.AddProfile<PolicyMapperProfile>();
                    cfg.AddProfile<SendMapperProfile>();
                    cfg.AddProfile<SsoConfigMapperProfile>();
                    cfg.AddProfile<SsoUserMapperProfile>();
                    cfg.AddProfile<TaxRateMapperProfile>();
                    cfg.AddProfile<TransactionMapperProfile>();
                    cfg.AddProfile<UserMapperProfile>();
                    cfg.AddProfile<PasswordHealthReportApplicationProfile>();
                })
            .CreateMapper()));

            fixture.Customize<ILogger<T>>(x => x.FromFactory(() => Substitute.For<ILogger<T>>()));

            var repo = fixture.Create<T>();
            list.Add(repo);
        }
        return list;
    }
}

public class IgnoreVirtualMembersCustomization : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException("context");
        }

        var pi = request as PropertyInfo;
        if (pi == null)
        {
            return new NoSpecimen();
        }

        if (pi.GetGetMethod().IsVirtual && pi.DeclaringType != typeof(GlobalSettings))
        {
            return null;
        }
        return new NoSpecimen();
    }
}
