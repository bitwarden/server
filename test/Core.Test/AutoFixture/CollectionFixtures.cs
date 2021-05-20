using System;
using AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;

namespace Bit.Core.Test.AutoFixture
{
    internal class CollectionDefaultIdAutoDataAttribute : CustomAutoDataAttribute
    {
        public CollectionDefaultIdAutoDataAttribute() : base(new SutProviderCustomization(), new Organization { CollectionId = default(Guid) })
        { }
    }

    internal class CollectionNonDefaultIdAutoDataAttribute : CustomAutoDataAttribute
    {
        public CollectionNonDefaultIdAutoDataAttribute() : base(new SutProviderCustomization(), new Organization { CollectionId = Guid.NewGuid() })
        { }
    }
}
