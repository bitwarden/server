using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.Test.AutoFixture;

internal class RequestValidationCustomization : ICustomization
{
    public string _userName { get; set; }

    public RequestValidationCustomization(string userName)
    {
        _userName = userName;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customize<ValidatedTokenRequest>(composer => composer
            .With(o => o.UserName, _userName)
            .With(o => o.RefreshToken, () => null)
            .With(o => o.ClientClaims, [])
            .With(o => o.Options, new Duende.IdentityServer.Configuration.IdentityServerOptions()));
    }
}

public class RequestValidationAttribute : CustomizeAttribute
{
    private readonly string _userName;

    public RequestValidationAttribute(string userName)
    {
        _userName = userName;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new RequestValidationCustomization(_userName);
    }
}
