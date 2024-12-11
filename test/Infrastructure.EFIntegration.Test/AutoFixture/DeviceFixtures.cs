using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class DeviceBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Device))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<Device>();
        return obj;
    }
}

internal class EfDevice : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new DeviceBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<DeviceRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
    }
}

internal class EfDeviceAutoDataAttribute : CustomAutoDataAttribute
{
    public EfDeviceAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfDevice()) { }
}

internal class InlineEfDeviceAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfDeviceAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfDevice) }, values) { }
}
