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

internal class AuthRequestBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(AuthRequest))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<AuthRequest>();
        return obj;
    }
}

internal class EfAuthRequest : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new AuthRequestBuilder());
        fixture.Customizations.Add(new DeviceBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<AuthRequestRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<DeviceRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
    }
}

internal class EfAuthRequestAutoDataAttribute : CustomAutoDataAttribute
{
    public EfAuthRequestAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfAuthRequest()) { }
}

internal class InlineEfAuthRequestAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfAuthRequestAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfAuthRequest) }, values) { }
}
