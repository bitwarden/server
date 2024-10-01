using AutoFixture;
using AutoFixture.Dsl;
using AutoFixture.Kernel;
using Bit.Core.NotificationCenter.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.AutoFixture;

public class NotificationCustomization(bool global) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Notification>(GetSpecimenBuilder);
    }

    public ISpecimenBuilder GetSpecimenBuilder(ICustomizationComposer<Notification> customizationComposer)
    {
        var postprocessComposer = customizationComposer.With(n => n.Id, Guid.NewGuid())
            .With(n => n.Global, global);

        postprocessComposer = global
            ? postprocessComposer.Without(n => n.UserId)
            : postprocessComposer.With(n => n.UserId, Guid.NewGuid());

        return global
            ? postprocessComposer.Without(n => n.OrganizationId)
            : postprocessComposer.With(n => n.OrganizationId, Guid.NewGuid());
    }
}

public class NotificationListCustomization(int count) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<List<Notification>>(composer => composer.FromFactory(() =>
        {
            var notificationCustomization = new NotificationCustomization(true);

            var notifications = new List<Notification>();
            for (var i = 0; i < count; i++)
            {
                var customizationComposer = fixture.Build<Notification>();
                var postprocessComposer =
                    customizationComposer.FromFactory(
                        notificationCustomization.GetSpecimenBuilder(customizationComposer));
                notifications.Add(postprocessComposer.Create());
            }

            return notifications;
        }));
    }
}

public class NotificationCustomizeAttribute(bool global = true)
    : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationCustomization(global);
}

public class NotificationListCustomizeAttribute(int count)
    : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationListCustomization(count);
}
