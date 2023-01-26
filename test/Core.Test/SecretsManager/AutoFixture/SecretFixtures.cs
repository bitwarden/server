using AutoFixture;
using Bit.Core.SecretsManager.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;

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

public class SecretCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SecretCustomization();
}
