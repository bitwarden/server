using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.GroupFixtures;

internal class GroupOrganizationAutoDataAttribute : CustomAutoDataAttribute
{
    public GroupOrganizationAutoDataAttribute() : base(
        new SutProviderCustomization(), new OrganizationCustomization { UseGroups = true })
    { }
}

internal class GroupOrganizationNotUseGroupsAutoDataAttribute : CustomAutoDataAttribute
{
    public GroupOrganizationNotUseGroupsAutoDataAttribute() : base(
        new SutProviderCustomization(), new OrganizationCustomization { UseGroups = false })
    { }
}
