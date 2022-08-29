using AutoFixture;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.Models.Data;

namespace Bit.Core.Test.AutoFixture.CipherFixtures;

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

internal class UserCipherCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new UserCipher();
}

internal class OrganizationCipherCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new OrganizationCipher();
}
