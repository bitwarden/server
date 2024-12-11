using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class OrganizationUserPolicyDetailsCustomization : ICustomization
{
    public PolicyType Type { get; set; }

    public OrganizationUserPolicyDetailsCustomization(PolicyType type)
    {
        Type = type;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationUserPolicyDetails>(composer =>
            composer
                .With(o => o.OrganizationId, Guid.NewGuid())
                .With(o => o.PolicyType, Type)
                .With(o => o.PolicyEnabled, true)
        );
    }
}

public class OrganizationUserPolicyDetailsAttribute : CustomizeAttribute
{
    private readonly PolicyType _type;

    public OrganizationUserPolicyDetailsAttribute(PolicyType type)
    {
        _type = type;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new OrganizationUserPolicyDetailsCustomization(_type);
    }
}
