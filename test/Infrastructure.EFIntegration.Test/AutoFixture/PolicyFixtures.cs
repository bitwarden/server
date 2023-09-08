using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class PolicyBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || type != typeof(Policy))
        {
            return new NoSpecimen();
        }

        var fixture = new Fixture();
        var obj = fixture.WithAutoNSubstitutions().Create<Policy>();
        return obj;
    }
}

internal class EfPolicyApplicableToUser : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new PolicyBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<PolicyRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<ProviderOrganizationRepository>());
    }
}

internal class EfPolicyApplicableToUserInlineAutoDataAttribute : InlineCustomAutoDataAttribute
{
    public EfPolicyApplicableToUserInlineAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization), typeof(EfPolicyApplicableToUser) }, values)
    { }
}
