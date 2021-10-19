using AutoFixture;
using TableModel = Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.GlobalSettingsFixtures;
using AutoMapper;
using Bit.Core.Models.EntityFramework;
using Bit.Core.Models;
using System.Collections.Generic;
using Bit.Core.Enums;
using AutoFixture.Kernel;
using System;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.TransactionFixtures;
using Bit.Core.Test.AutoFixture.Relays;

namespace Bit.Core.Test.AutoFixture.CollectionFixtures
{
    internal class CollectionBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.Collection))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.Collection>();
            return obj;
        }
    }

    internal class EfCollection: ICustomization 
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
        public CollectionAutoDataAttribute() : base(new SutProviderCustomization(), new Bit.Core.Test.AutoFixture.OrganizationFixtures.Organization())
        { }
    }
}
