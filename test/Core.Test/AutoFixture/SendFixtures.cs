using System;
using AutoFixture;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.SendFixtures
{
    internal class OrganizationSend : ICustomization
    {
        public Guid? OrganizationId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Send>(composer => composer
                .With(s => s.OrganizationId, OrganizationId ?? Guid.NewGuid())
                .Without(s => s.UserId));
        }
    }

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

    internal class UserSendAutoDataAttribute : CustomAutoDataAttribute
    {
        public UserSendAutoDataAttribute(string userId = null) : base(new SutProviderCustomization(),
            new UserSend { UserId = userId == null ? (Guid?)null : new Guid(userId) })
        { }
    }
    internal class InlineUserSendAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineUserSendAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(UserSend) }, values)
        { }
    }

    internal class InlineKnownUserSendAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineKnownUserSendAutoDataAttribute(string userId, params object[] values) : base(new ICustomization[]
            { new SutProviderCustomization(), new UserSend { UserId = new Guid(userId) } }, values)
        { }
    }

    internal class OrganizationSendAutoDataAttribute : CustomAutoDataAttribute
    {
        public OrganizationSendAutoDataAttribute(string organizationId = null) : base(new SutProviderCustomization(),
            new OrganizationSend { OrganizationId = organizationId == null ? (Guid?)null : new Guid(organizationId) })
        { }
    }

    internal class InlineOrganizationSendAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineOrganizationSendAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(OrganizationSend) }, values)
        { }
    }
}
