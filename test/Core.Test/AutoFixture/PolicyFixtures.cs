using System;
using System.Collections.Generic;
using System.Text;
using AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;

namespace Bit.Core.Test.AutoFixture
{
    internal class PolicyCustomization : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Core.Models.Table.Policy>(composer => composer
                .With(p => p.Id, Guid.NewGuid())
                .With(p => p.OrganizationId, Guid.NewGuid())
                .With(p => p.Type, Enums.PolicyType.DisableSend)
                .With(p => p.Data, "")
                .With(p => p.Enabled, true));
        }
    }

    internal class PolicyAutoDataAttribute : CustomAutoDataAttribute
    {
        public PolicyAutoDataAttribute() : base(
            new SutProviderCustomization(), new PolicyCustomization(), new Organization())
        { }
    }
}
