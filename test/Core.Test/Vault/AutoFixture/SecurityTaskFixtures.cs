using AutoFixture;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Vault.AutoFixture;

public class SecurityTaskFixtures : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<SecurityTask>(composer =>
            composer
                .With(task => task.Id, Guid.NewGuid())
                .With(task => task.OrganizationId, Guid.NewGuid())
                .With(task => task.Status, SecurityTaskStatus.Pending)
                .Without(x => x.CipherId)
        );
    }
}

public class SecurityTaskCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SecurityTaskFixtures();
}
