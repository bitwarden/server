using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Commercial.Core.Test.AdminConsole.AutoFixture;

internal class ProviderUser : ICustomization
{
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }

    public ProviderUser(ProviderUserStatusType status, ProviderUserType type)
    {
        Status = status;
        Type = type;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<Bit.Core.AdminConsole.Entities.Provider.ProviderUser>(composer =>
            composer.With(o => o.Type, Type).With(o => o.Status, Status)
        );
    }
}

public class ProviderUserAttribute : CustomizeAttribute
{
    private readonly ProviderUserStatusType _status;
    private readonly ProviderUserType _type;

    public ProviderUserAttribute(
        ProviderUserStatusType status = ProviderUserStatusType.Confirmed,
        ProviderUserType type = ProviderUserType.ProviderAdmin
    )
    {
        _status = status;
        _type = type;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new ProviderUser(_status, _type);
    }
}
