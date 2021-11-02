using AutoFixture;
using TableModel = Bit.Core.Models.Table;
using AutoFixture.Kernel;
using System;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;

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
            if (type == null || type != typeof(TableModel.OrganizationSponsorship))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.OrganizationSponsorship>();
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
        public EfOrganizationSponsorshipAutoDataAttribute() : base(new SutProviderCustomization(), new EfOrganizationSponsorship())
        { }
    }

    internal class InlineEfOrganizationSponsorshipAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfOrganizationSponsorshipAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfOrganizationSponsorship) }, values)
        { }
    }
}
