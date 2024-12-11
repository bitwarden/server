using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class CollectionCipherBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(CollectionCipher))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<CollectionCipher>();
        return obj;
    }
}

internal class EfCollectionCipher : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new CollectionCipherBuilder());
        fixture.Customizations.Add(new CollectionBuilder());
        fixture.Customizations.Add(new CipherBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CollectionCipherRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CollectionRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CipherRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
    }
}

internal class EfCollectionCipherAutoDataAttribute : CustomAutoDataAttribute
{
    public EfCollectionCipherAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfCollectionCipher()) { }
}

internal class InlineEfCollectionCipherAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public InlineEfCollectionCipherAutoDataAttribute(params object[] values)
        : base(new[] { typeof(SutProviderCustomization), typeof(EfCollectionCipher) }, values) { }
}
