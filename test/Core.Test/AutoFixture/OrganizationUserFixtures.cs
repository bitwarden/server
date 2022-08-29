using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Test.AutoFixture.OrganizationUserFixtures;

public class OrganizationUserCustomization : ICustomization
{
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType Type { get; set; }

    public OrganizationUserCustomization(OrganizationUserStatusType status, OrganizationUserType type)
    {
        Status = status;
        Type = type;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<OrganizationUser>(composer => composer
            .With(o => o.Type, Type)
            .With(o => o.Status, Status));
    }
}

public class OrganizationUserAttribute : CustomizeAttribute
{
    private readonly OrganizationUserStatusType _status;
    private readonly OrganizationUserType _type;

    public OrganizationUserAttribute(
        OrganizationUserStatusType status = OrganizationUserStatusType.Confirmed,
        OrganizationUserType type = OrganizationUserType.User)
    {
        _status = status;
        _type = type;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new OrganizationUserCustomization(_status, _type);
    }
}
