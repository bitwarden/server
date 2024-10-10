#nullable enable
using AutoFixture;
using Bit.Core.NotificationCenter.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.NotificationCenter.AutoFixture;

public class NotificationCustomization(bool global) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Notification>(composer =>
        {
            var postprocessComposer = composer.With(n => n.Id, Guid.NewGuid())
                .With(n => n.Global, global);

            postprocessComposer = global
                ? postprocessComposer.Without(n => n.UserId)
                : postprocessComposer.With(n => n.UserId, Guid.NewGuid());

            return global
                ? postprocessComposer.Without(n => n.OrganizationId)
                : postprocessComposer.With(n => n.OrganizationId, Guid.NewGuid());
        });
    }
}

public class NotificationCustomizeAttribute(bool global = true) : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new NotificationCustomization(global);
}
