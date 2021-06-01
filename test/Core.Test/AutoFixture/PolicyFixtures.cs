using System;
using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Enums;

namespace Bit.Core.Test.AutoFixture.OrganizationUserFixtures
{
    internal class Policy : ICustomization
    {
        public PolicyType Type { get; set; }

        public Policy(PolicyType type)
        {
            Type = type;
        }
        
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Core.Models.Table.Policy>(composer => composer
                .With(o => o.OrganizationId, Guid.NewGuid())
                .With(o => o.Type, Type)
                .With(o => o.Enabled, true));
        }
    }

    public class PolicyAttribute : CustomizeAttribute
    {
        private readonly PolicyType _type;

        public PolicyAttribute(PolicyType type)
        {
            _type = type;
        }

        public override ICustomization GetCustomization(ParameterInfo parameter)
        {
            return new Policy(_type);
        }
    }
}
