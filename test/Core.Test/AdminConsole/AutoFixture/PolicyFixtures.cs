using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class PolicyCustomization : ICustomization
{
    public PolicyType Type { get; set; }
    public bool Enabled { get; set; }

    public PolicyCustomization(PolicyType type, bool enabled)
    {
        Type = type;
        Enabled = enabled;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<Policy>(composer =>
            composer
                .With(o => o.OrganizationId, Guid.NewGuid())
                .With(o => o.Type, Type)
                .With(o => o.Enabled, Enabled)
        );
    }
}

public class PolicyAttribute : CustomizeAttribute
{
    private readonly PolicyType _type;
    private readonly bool _enabled;

    public PolicyAttribute(PolicyType type, bool enabled = true)
    {
        _type = type;
        _enabled = enabled;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new PolicyCustomization(_type, _enabled);
    }
}
