using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

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
        if (type == null || type != typeof(Transaction))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        if (!OrganizationOwned)
        {
            fixture.Customize<Transaction>(composer => composer.Without(c => c.OrganizationId));
        }
        fixture.Customizations.Add(new MaxLengthStringRelay());
        var obj = fixture.WithAutoNSubstitutions().Create<Transaction>();
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
    public EfUserTransactionAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfTransaction()) { }
}

internal class EfOrganizationTransactionAutoDataAttribute : CustomAutoDataAttribute
{
    public EfOrganizationTransactionAutoDataAttribute()
        : base(new SutProviderCustomization(), new EfTransaction() { OrganizationOwned = true }) { }
}
