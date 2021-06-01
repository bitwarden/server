using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;

namespace Bit.Core.Test.AutoFixture
{

    internal class CollectionAutoDataAttribute : CustomAutoDataAttribute
    {
        public CollectionAutoDataAttribute() : base(new SutProviderCustomization(), new Organization())
        { }
    }
}
