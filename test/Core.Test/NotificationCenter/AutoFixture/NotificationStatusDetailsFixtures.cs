#nullable enable
using AutoFixture;
using Bit.Core.NotificationCenter.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.AutoFixture;

public class NotificationStatusDetailsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<NotificationStatusDetails>(composer => composer.With(n => n.Id, Guid.NewGuid())
            .With(ns => ns.UserId, Guid.NewGuid())
            .With(ns => ns.OrganizationId, Guid.NewGuid())
            .With(ns => ns.NotificationStatusUserId, Guid.NewGuid()));
    }
}

public class NotificationStatusDetailsCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationStatusDetailsCustomization();
}
