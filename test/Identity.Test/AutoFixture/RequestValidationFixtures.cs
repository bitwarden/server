using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.Test.AutoFixture;

internal class ValidatedTokenRequestCustomization : ICustomization
{
    public ValidatedTokenRequestCustomization()
    { }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<ValidatedTokenRequest>(composer => composer
            .With(o => o.RefreshToken, () => null)
            .With(o => o.ClientClaims, [])
            .With(o => o.Options, new Duende.IdentityServer.Configuration.IdentityServerOptions()));
    }
}

public class ValidatedTokenRequestAttribute : CustomizeAttribute
{
    public ValidatedTokenRequestAttribute()
    { }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new ValidatedTokenRequestCustomization();
    }
}
