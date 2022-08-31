using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class CollectionBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Collection))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<Collection>();
        return obj;
    }
}

internal class EfCollection : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new CollectionBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CollectionRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfCollectionCustomize : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new EfCollection();
}
