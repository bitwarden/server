using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class OrganizationPolicyDetailsCustomization(
    PolicyType policyType,
    OrganizationUserType userType,
    bool isProvider,
    OrganizationUserStatusType userStatus) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationPolicyDetails>(composer => composer
            .With(o => o.PolicyType, policyType)
            .With(o => o.OrganizationUserType, userType)
            .With(o => o.IsProvider, isProvider)
            .With(o => o.OrganizationUserStatus, userStatus)
            .Without(o => o.PolicyData)); // avoid autogenerating invalid json data
    }
}

public class OrganizationPolicyDetailsAttribute(
    PolicyType policyType,
    OrganizationUserType userType = OrganizationUserType.User,
    bool isProvider = false,
    OrganizationUserStatusType userStatus = OrganizationUserStatusType.Confirmed) : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter)
        => new OrganizationPolicyDetailsCustomization(policyType, userType, isProvider, userStatus);
}
