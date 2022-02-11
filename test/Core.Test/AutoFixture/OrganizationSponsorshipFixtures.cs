using System;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures
{
    internal class OrganizationSponsorshipBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(OrganizationSponsorship))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            var obj = fixture.WithAutoNSubstitutions().Create<OrganizationSponsorship>();
            return obj;
        }
    }

    internal class EfOrganizationSponsorship : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new OrganizationSponsorshipBuilder());
            fixture.Customizations.Add(new OrganizationUserBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationSponsorshipRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        }
    }

    internal class EfOrganizationSponsorshipAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfOrganizationSponsorshipAutoDataAttribute() : base(new SutProviderCustomization(), new EfOrganizationSponsorship(), new EfOrganization())
        { }
    }

    internal class InlineEfOrganizationSponsorshipAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfOrganizationSponsorshipAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfOrganizationSponsorship), typeof(EfOrganization) }, values)
        { }
    }
}
