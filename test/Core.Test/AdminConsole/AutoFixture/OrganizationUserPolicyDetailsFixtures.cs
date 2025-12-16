using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

internal class OrganizationUserPolicyDetailsCustomization : ICustomization
{
    public PolicyType Type { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType UserType { get; set; }
    public bool IsProvider { get; set; }

    public OrganizationUserPolicyDetailsCustomization(PolicyType type, OrganizationUserStatusType status, OrganizationUserType userType, bool isProvider)
    {
        Type = type;
        Status = status;
        UserType = userType;
        IsProvider = isProvider;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationUserPolicyDetails>(composer => composer
            .With(o => o.OrganizationId, Guid.NewGuid())
            .With(o => o.PolicyType, Type)
            .With(o => o.OrganizationUserStatus, Status)
            .With(o => o.OrganizationUserType, UserType)
            .With(o => o.IsProvider, IsProvider)
            .With(o => o.PolicyEnabled, true));
    }
}

public class OrganizationUserPolicyDetailsAttribute : CustomizeAttribute
{
    private readonly PolicyType _type;
    private readonly OrganizationUserStatusType _status;
    private readonly OrganizationUserType _userType;
    private readonly bool _isProvider;

    public OrganizationUserPolicyDetailsAttribute(PolicyType type) : this(type, OrganizationUserStatusType.Accepted, OrganizationUserType.User, false)
    {
        _type = type;
    }

    public OrganizationUserPolicyDetailsAttribute(PolicyType type, OrganizationUserStatusType status, OrganizationUserType userType, bool isProvider)
    {
        _type = type;
        _status = status;
        _userType = userType;
        _isProvider = isProvider;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new OrganizationUserPolicyDetailsCustomization(_type, _status, _userType, _isProvider);
    }
}
