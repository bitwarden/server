using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class InstallationBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Installation))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var obj = fixture.WithAutoNSubstitutions().Create<Installation>();
        return obj;
    }
}

internal class EfInstallation : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new InstallationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<InstallationRepository>());
    }
}

internal class EfInstallationAutoDataAttribute : CustomAutoDataAttribute
{
    public EfInstallationAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfInstallation()) { }
}

internal class InlineEfInstallationAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfInstallationAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfInstallation) }, values) { }
}
