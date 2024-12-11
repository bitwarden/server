using AutoFixture;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class EfUser : UserFixture
{
    public override void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        base.Customize(fixture);
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<SsoUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfUserAutoDataAttribute : CustomAutoDataAttribute
{
    public EfUserAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfUser()) { }
}

internal class InlineEfUserAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfUserAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfUser) }, values) { }
}
