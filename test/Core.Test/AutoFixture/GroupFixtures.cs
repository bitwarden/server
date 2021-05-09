using System;
using AutoFixture;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture
{
    internal class GroupOrganization : ICustomization
    {
        public bool UseGroups { get; set; }

        public void Customize(IFixture fixture)
        {
            var organizationId = Guid.NewGuid();

            fixture.Customize<Organization>(composer => composer
                .With(o => o.Id, organizationId)
                .With(o => o.UseGroups, UseGroups));

            fixture.Customize<Group>(composer => composer.With(g => g.OrganizationId, organizationId));
        }
    }

    internal class GroupOrganizationAutoDataAttribute : CustomAutoDataAttribute
    {
        public GroupOrganizationAutoDataAttribute() : base(
            new SutProviderCustomization(), new GroupOrganization { UseGroups = true })
        { }
    }

    internal class GroupOrganizationNotUseGroupsAutoDataAttribute : CustomAutoDataAttribute
    {
        public GroupOrganizationNotUseGroupsAutoDataAttribute() : base(
            new SutProviderCustomization(), new GroupOrganization { UseGroups = false })
        { }
    }
}
