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

namespace Bit.Core.Test.AutoFixture.CollectionCipherFixtures
{
    internal class CollectionCipherBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.CollectionCipher))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.CollectionCipher>();
            return obj;
        }
    }

    internal class EfCollectionCipher: ICustomization 
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new CollectionCipherBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<CollectionCipherRepository>());
        }
    }

    internal class EfCollectionCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfCollectionCipherAutoDataAttribute() : base(new SutProviderCustomization(), new EfCollectionCipher())
        { }
    }

    internal class InlineEfCollectionCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfCollectionCipherAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfCollectionCipher) }, values)
        { }
    }
}
