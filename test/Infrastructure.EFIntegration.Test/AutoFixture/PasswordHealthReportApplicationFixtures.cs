using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Tools.Entities;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Tools.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class PasswordHealthReportApplicationBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(PasswordHealthReportApplication))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var obj = fixture.WithAutoNSubstitutions().Create<PasswordHealthReportApplication>();
        return obj;
    }
}

internal class EfPasswordHealthReportApplication : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new PasswordHealthReportApplicationBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<PasswordHealthReportApplicationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfPasswordHealthReportApplicationApplicableToUser : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new PasswordHealthReportApplicationBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<PasswordHealthReportApplicationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderOrganizationRepository>());
    }
}

internal class EfPasswordHealthReportApplicationAutoDataAttribute : CustomAutoDataAttribute
{
    public EfPasswordHealthReportApplicationAutoDataAttribute() : base(new SutProviderCustomization(), new EfPasswordHealthReportApplication())
    { }
}

internal class EfPasswordHealthReportApplicationApplicableToUserInlineAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public EfPasswordHealthReportApplicationApplicableToUserInlineAutoDataAttribute(params object[] values) :
        base(new[] { typeof(SutProviderCustomization), typeof(EfPasswordHealthReportApplicationApplicableToUser) }, values)
    { }
}

internal class InlineEfPasswordHealthReportApplicationAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfPasswordHealthReportApplicationAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
        typeof(EfPolicy) }, values)
    { }
}
