using System;
using AutoFixture;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Core.Models.Data;

namespace Bit.Core.Test.AutoFixture.CipherFixtures
{
    internal class OrganizationCipher : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Cipher>(composer => composer
                .With(c => c.OrganizationId, Guid.NewGuid())
                .Without(c => c.UserId));
            fixture.Customize<CipherDetails>(composer => composer
                .With(c => c.OrganizationId, Guid.NewGuid())
                .Without(c => c.UserId));
        }
    }

    internal class UserCipher : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Cipher>(composer => composer
                .With(c => c.UserId, Guid.NewGuid())
                .Without(c => c.OrganizationId));
            fixture.Customize<CipherDetails>(composer => composer
                .With(c => c.UserId, Guid.NewGuid())
                .Without(c => c.OrganizationId));
        }
    }

    internal class UserCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public UserCipherAutoDataAttribute() : base(typeof(SutProviderCustomization), typeof(UserCipher))
        { }
    }
    internal class InlineUserCipherAutoData : InlineCustomAutoDataAttribute
    {
        public InlineUserCipherAutoData(params object[] values) : base(new[] { typeof(SutProviderCustomization), typeof(UserCipher) }, values)
        { }
    }
}
