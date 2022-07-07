using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.CollectionFixtures
{
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

    internal class EfCollectionAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfCollectionAutoDataAttribute() : base(new SutProviderCustomization(), new EfCollection())
        { }
    }

    internal class InlineEfCollectionAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfCollectionAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfCollection) }, values)
        { }
    }

    internal class CollectionAutoDataAttribute : CustomAutoDataAttribute
    {
        public CollectionAutoDataAttribute() : base(new SutProviderCustomization(), new OrganizationCustomization())
        { }
    }
}
