using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.CollectionFixtures;

internal class CollectionAutoDataAttribute : CustomAutoDataAttribute
{
    public CollectionAutoDataAttribute() : base(new SutProviderCustomization(), new OrganizationCustomization())
    { }
}
