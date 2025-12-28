using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit3;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class PolicyDetailsCustomization(
    PolicyType policyType,
    OrganizationUserType userType,
    bool isProvider,
    OrganizationUserStatusType userStatus) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<PolicyDetails>(composer => composer
            .With(o => o.PolicyType, policyType)
            .With(o => o.OrganizationUserType, userType)
            .With(o => o.IsProvider, isProvider)
            .With(o => o.OrganizationUserStatus, userStatus)
            .Without(o => o.PolicyData)); // avoid autogenerating invalid json data
    }
}

public class PolicyDetailsAttribute(
    PolicyType policyType,
    OrganizationUserType userType = OrganizationUserType.User,
    bool isProvider = false,
    OrganizationUserStatusType userStatus = OrganizationUserStatusType.Confirmed) : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter)
        => new PolicyDetailsCustomization(policyType, userType, isProvider, userStatus);
}


