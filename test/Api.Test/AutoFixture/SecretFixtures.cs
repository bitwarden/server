using AutoFixture;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Api.Test.AutoFixture;

public class SecretCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var secretId = Guid.NewGuid();

        fixture.Customize<Secret>(composer => composer
            .With(o => o.Id, secretId)
            .Without(s => s.Projects));
    }
}

internal class SecretCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SecretCustomization();
}
