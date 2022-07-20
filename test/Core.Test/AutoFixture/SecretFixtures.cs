using AutoFixture;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.CollectionFixtures
{
    internal class EfSecret : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<SecretRepository>());
        }
    }

    internal class EfSecretAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfSecretAutoDataAttribute() : base(new SutProviderCustomization(), new EfSecret())
        { }
    }

    internal class InlineEfSecretAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfSecretAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfSecret) }, values)
        { }
    }
}
