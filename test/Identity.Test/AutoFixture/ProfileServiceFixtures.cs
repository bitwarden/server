using System.Reflection;
using System.Security.Claims;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Core.Auth.Identity;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.Test.AutoFixture;

internal class ProfileDataRequestContextCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<ProfileDataRequestContext>(composer => composer
            .With(o => o.Subject, new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", Guid.NewGuid().ToString()),
                  new Claim("name", "Test User"),
                  new Claim("email", "test@example.com")
            ])))
            .With(o => o.Client, new Client { ClientId = "web" })
            .With(o => o.ValidatedRequest, () => null)
            .With(o => o.RequestedResources, new ResourceValidationResult())
            .With(o => o.IssuedClaims, [])
            .Without(o => o.Caller));
    }
}

public class ProfileDataRequestContextAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new ProfileDataRequestContextCustomization();
    }
}

internal class IsActiveContextCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<IsActiveContext>(composer => composer
            .With(o => o.Subject, new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", Guid.NewGuid().ToString()),
                  new Claim(Claims.SecurityStamp, "test-security-stamp")
            ])))
            .With(o => o.Client, new Client { ClientId = "web" })
            .With(o => o.IsActive, false)
            .Without(o => o.Caller));
    }
}

public class IsActiveContextAttribute : CustomizeAttribute
{
    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new IsActiveContextCustomization();
    }
}
