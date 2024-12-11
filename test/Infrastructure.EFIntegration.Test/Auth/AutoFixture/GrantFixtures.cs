using AutoFixture;
using AutoFixture.Kernel;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Auth.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.AutoFixture;

internal class GrantBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Grant))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<Grant>();
        return obj;
    }
}

internal class EfGrant : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new GrantBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<GrantRepository>());
    }
}

internal class EfGrantAutoDataAttribute : CustomAutoDataAttribute
{
    public EfGrantAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfGrant()) { }
}

internal class InlineEfGrantAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfGrantAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfGrant) }, values) { }
}
