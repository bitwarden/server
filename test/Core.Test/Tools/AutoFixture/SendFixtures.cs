using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Tools.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.Tools.AutoFixture.SendFixtures;

internal class UserSend : ICustomization
{
    public Guid? UserId { get; set; }
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Send>(composer => composer
            .With(s => s.UserId, UserId ?? Guid.NewGuid())
            .Without(s => s.OrganizationId));
    }
}

internal class UserSendCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new UserSend();
}

internal class NewUserSend : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Send>(composer => composer
            .With(s => s.Id, Guid.Empty)
            .Without(s => s.OrganizationId));
    }
}

internal class NewUserSendCustomizeAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameterInfo)
        => new NewUserSend();
}

