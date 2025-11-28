#nullable enable
using AutoFixture;
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.AutoFixture;

public class NotificationStatusDetailsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<NotificationStatusDetails>(composer =>
        {
            return composer.With(n => n.Id, Guid.NewGuid())
                .With(n => n.UserId, Guid.NewGuid())
                .With(n => n.OrganizationId, Guid.NewGuid());
        });
    }
}

public class NotificationStatusDetailsListCustomization(int count) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var customization = new NotificationStatusDetailsCustomization();
        fixture.Customize<IEnumerable<NotificationStatusDetails>>(composer => composer.FromFactory(() =>
        {
            var notifications = new List<NotificationStatusDetails>();
            for (var i = 0; i < count; i++)
            {
                customization.Customize(fixture);
                var notificationStatusDetails = fixture.Create<NotificationStatusDetails>();
                notifications.Add(notificationStatusDetails);
            }

            return notifications;
        }));
    }
}

public class NotificationStatusDetailsCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationStatusDetailsCustomization();
}

public class NotificationStatusDetailsListCustomizeAttribute(int count) : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationStatusDetailsListCustomization(count);
}
