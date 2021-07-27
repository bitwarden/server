using AutoFixture;
using TableModel = Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.GlobalSettingsFixtures;
using AutoFixture.Kernel;
using System;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Fixtures = Bit.Core.Test.AutoFixture.OrganizationFixtures;

namespace Bit.Core.Test.AutoFixture.GroupFixtures
{
    internal class GroupOrganizationAutoDataAttribute : CustomAutoDataAttribute
    {
        public GroupOrganizationAutoDataAttribute() : base(
            new SutProviderCustomization(), new Fixtures.Organization { UseGroups = true })
        { }
    }

    internal class GroupOrganizationNotUseGroupsAutoDataAttribute : CustomAutoDataAttribute
    {
        public GroupOrganizationNotUseGroupsAutoDataAttribute() : base(
            new SutProviderCustomization(), new Bit.Core.Test.AutoFixture.OrganizationFixtures.Organization { UseGroups = false })
        { }
    }

    internal class GroupBuilder: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(TableModel.Group))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            var obj = fixture.WithAutoNSubstitutions().Create<TableModel.Group>();
            return obj;
        }
    }

    internal class EfGroup: ICustomization 
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new IgnoreVirtualMembersCustomization());
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new GroupBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<GroupRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        }
    }

    internal class EfGroupAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfGroupAutoDataAttribute() : base(new SutProviderCustomization(), new EfGroup())
        { }
    }

    internal class InlineEfGroupAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfGroupAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfGroup) }, values)
        { }
    }
}
