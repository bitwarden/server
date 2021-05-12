using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;

namespace Bit.Core.Test.AutoFixture
{
    internal class GroupOrganizationAutoDataAttribute : CustomAutoDataAttribute
    {
        public GroupOrganizationAutoDataAttribute() : base(
            new SutProviderCustomization(), new Organization { UseGroups = true })
        { }
    }

    internal class GroupOrganizationNotUseGroupsAutoDataAttribute : CustomAutoDataAttribute
    {
        public GroupOrganizationNotUseGroupsAutoDataAttribute() : base(
            new SutProviderCustomization(), new Organization { UseGroups = false })
        { }
    }
}
