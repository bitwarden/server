using System;
using AutoFixture;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Core.Models.Data;

namespace Bit.Core.Test.AutoFixture.CipherFixtures
{
    internal class OrganizationCipher : ICustomization
    {
        public Guid? OrganizationId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Cipher>(composer => composer
                .With(c => c.OrganizationId, OrganizationId ?? Guid.NewGuid())
                .Without(c => c.UserId));
            fixture.Customize<CipherDetails>(composer => composer
                .With(c => c.OrganizationId, Guid.NewGuid())
                .Without(c => c.UserId));
        }
    }

    internal class UserCipher : ICustomization
    {
        public Guid? UserId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Cipher>(composer => composer
                .With(c => c.UserId, UserId ?? Guid.NewGuid())
                .Without(c => c.OrganizationId));
            fixture.Customize<CipherDetails>(composer => composer
                .With(c => c.UserId, Guid.NewGuid())
                .Without(c => c.OrganizationId));
        }
    }

    internal class UserCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public UserCipherAutoDataAttribute(string userId = null) : base(new SutProviderCustomization(),
            new UserCipher { UserId = userId == null ? (Guid?)null : new Guid(userId) })
        { }
    }
    internal class InlineUserCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineUserCipherAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(UserCipher) }, values)
        { }
    }

    internal class InlineKnownUserCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineKnownUserCipherAutoDataAttribute(string userId, params object[] values) : base(new ICustomization[]
            { new SutProviderCustomization(), new UserCipher { UserId = new Guid(userId) } }, values)
    { }
    }

    internal class OrganizationCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public OrganizationCipherAutoDataAttribute(string organizationId = null) : base(new SutProviderCustomization(),
            new OrganizationCipher { OrganizationId = organizationId == null ? (Guid?)null : new Guid(organizationId) })
        { }
    }

    internal class InlineOrganizationCipherAuthoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineOrganizationCipherAuthoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(OrganizationCipher) }, values)
        { }
    }
}
