using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Auth.Entities;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.AutoFixture;

internal class EmergencyAccessBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(EmergencyAccess))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.Create<EmergencyAccess>();
        return obj;
    }
}

internal class EfEmergencyAccess : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // TODO: Make a base EF Customization with IgnoreVirtualMembers/GlobalSettings/All repos and inherit
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new EmergencyAccessBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<EmergencyAccessRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
    }
}

internal class EfEmergencyAccessAutoDataAttribute : CustomAutoDataAttribute
{
    public EfEmergencyAccessAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfEmergencyAccess()) { }
}

internal class InlineEfEmergencyAccessAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfEmergencyAccessAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfEmergencyAccess) }, values) { }
}
