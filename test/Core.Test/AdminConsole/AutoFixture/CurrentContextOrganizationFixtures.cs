using AutoFixture;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AdminConsole.AutoFixture;

public class CurrentContextOrganizationCustomization : ICustomization
{
    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; } = new();
    public bool AccessSecretsManager { get; set; }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<CurrentContextOrganization>(composer =>
            composer
                .With(o => o.Id, Id)
                .With(o => o.Type, Type)
                .With(o => o.Permissions, Permissions)
                .With(o => o.AccessSecretsManager, AccessSecretsManager)
        );
    }
}

public class CurrentContextOrganizationCustomizeAttribute : BitCustomizeAttribute
{
    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; } = OrganizationUserType.User;
    public Permissions Permissions { get; set; } = new();
    public bool AccessSecretsManager { get; set; } = false;

    public override ICustomization GetCustomization() =>
        new CurrentContextOrganizationCustomization()
        {
            Id = Id,
            Type = Type,
            Permissions = Permissions,
            AccessSecretsManager = AccessSecretsManager,
        };
}
