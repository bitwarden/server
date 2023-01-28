using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class SendBuilder : ISpecimenBuilder
{
    public bool OrganizationOwned { get; set; }
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Send))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        fixture.Customizations.Insert(0, new MaxLengthStringRelay());
        if (!OrganizationOwned)
        {
            fixture.Customize<Send>(composer => composer
                    .Without(c => c.OrganizationId));
        }
        var obj = fixture.WithAutoNSubstitutions().Create<Send>();
        return obj;
    }
}

internal class EfSend : ICustomization
{
    public bool OrganizationOwned { get; set; }
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new SendBuilder());
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<SendRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
    }
}

internal class EfUserSendAutoDataAttribute : CustomAutoDataAttribute
{
    public EfUserSendAutoDataAttribute() : base(new SutProviderCustomization(), new EfSend())
    { }
}

internal class EfOrganizationSendAutoDataAttribute : CustomAutoDataAttribute
{
    public EfOrganizationSendAutoDataAttribute() : base(new SutProviderCustomization(), new EfSend()
    {
        OrganizationOwned = true,
    })
    { }
}
