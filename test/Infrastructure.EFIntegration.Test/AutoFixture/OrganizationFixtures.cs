using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class OrganizationBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Organization))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var providers = fixture.Create<Dictionary<TwoFactorProviderType, TwoFactorProvider>>();
        var organization = new Fixture().WithAutoNSubstitutions().Create<Organization>();
        organization.SetTwoFactorProviders(providers);
        return organization;
    }
}

internal class EfOrganization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfOrganizationAutoDataAttribute : CustomAutoDataAttribute
{
    public EfOrganizationAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfOrganization()) { }
}

internal class InlineEfOrganizationAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfOrganizationAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfOrganization) }, values) { }
}
