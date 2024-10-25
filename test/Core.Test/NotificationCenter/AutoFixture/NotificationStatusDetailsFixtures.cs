#nullable enable
using AutoFixture;
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.AutoFixture;

public class NotificationStatusDetailsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<NotificationStatusDetails>(composer => composer.With(n => n.Id, Guid.NewGuid())
            .With(n => n.UserId, Guid.NewGuid())
            .With(n => n.OrganizationId, Guid.NewGuid()));
    }
}

public class NotificationStatusDetailsCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationStatusDetailsCustomization();
}
