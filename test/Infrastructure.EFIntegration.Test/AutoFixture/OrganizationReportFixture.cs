using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Dirt.Entities;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class OrganizationReportBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(OrganizationReport))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var obj = fixture.WithAutoNSubstitutions().Create<OrganizationReport>();
        return obj;
    }
}

internal class EfOrganizationReport : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new OrganizationReportBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationReportRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfOrganizationReportApplicableToUser : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new OrganizationReportBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationReportRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderOrganizationRepository>());
    }
}

internal class EfOrganizationReportAutoDataAttribute : CustomAutoDataAttribute
{
    public EfOrganizationReportAutoDataAttribute() : base(new SutProviderCustomization(), new EfOrganizationReport()) { }
}

internal class EfOrganizationReportApplicableToUserInlineAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public EfOrganizationReportApplicableToUserInlineAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfOrganizationReportApplicableToUser) }, values) { }
}

internal class InlineEfOrganizationReportAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfOrganizationReportAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
        typeof(EfPolicy) }, values)
    { }
}
