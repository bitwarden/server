using System;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Models.EntityFramework;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Test.AutoFixture.TransactionFixtures
{
    internal class TransactionBuilder : ISpecimenBuilder
    {
        public bool OrganizationOwned { get; set; }
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.Transaction))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            if (!OrganizationOwned)
            {
                fixture.Customize<Transaction>(composer => composer
                        .Without(c => c.OrganizationId));
            }
            fixture.Customizations.Add(new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.Transaction>();
            return obj;
        }
    }

    internal class EfTransaction : ICustomization
    {
        public bool OrganizationOwned { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new TransactionBuilder());
            fixture.Customizations.Add(new UserBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<TransactionRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        }
    }

    internal class EfUserTransactionAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfUserTransactionAutoDataAttribute() : base(new SutProviderCustomization(), new EfTransaction())
        { }
    }

    internal class EfOrganizationTransactionAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfOrganizationTransactionAutoDataAttribute() : base(new SutProviderCustomization(), new EfTransaction()
        {
            OrganizationOwned = true,
        })
        { }
    }
}

