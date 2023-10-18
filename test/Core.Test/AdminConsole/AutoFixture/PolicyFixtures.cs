using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class PolicyCustomization : ICustomization
{
    public PolicyType Type { get; set; }

    public PolicyCustomization(PolicyType type)
    {
        Type = type;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<Policy>(composer => composer
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
        return new PolicyCustomization(_type);
    }
}
