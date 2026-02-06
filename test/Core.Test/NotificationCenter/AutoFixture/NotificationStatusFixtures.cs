#nullable enable
using AutoFixture;
using Bit.Core.NotificationCenter.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.AutoFixture;

public class NotificationStatusCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<NotificationStatus>(composer => composer.With(ns => ns.NotificationId, Guid.NewGuid())
            .With(ns => ns.UserId, Guid.NewGuid()));
    }
}

public class NotificationStatusCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationStatusCustomization();
}
