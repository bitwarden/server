using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.Test.AutoFixture;

internal class ValidatedTokenRequestCustomization : ICustomization
{
    public ValidatedTokenRequestCustomization()
    {
    }

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
    {
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new ValidatedTokenRequestCustomization();
    }
}

internal class CustomValidatorRequestContextCustomization : ICustomization
{
    public CustomValidatorRequestContextCustomization()
    {
    }

    /// <summary>
    /// Specific context members like <see cref="CustomValidatorRequestContext.RememberMeRequested" />,
    /// <see cref="CustomValidatorRequestContext.TwoFactorRecoveryRequested"/>, and
    /// <see cref="CustomValidatorRequestContext.SsoRequired" /> should initialize false,
    /// and are made truthy in context upon evaluation of a request. Do not allow AutoFixture to eagerly make these
    /// truthy; that is the responsibility of the <see cref="Bit.Identity.IdentityServer.RequestValidators.BaseRequestValidator{T}" />.
    /// ValidationErrorResult and CustomResponse should also be null initially; they are hydrated during the validation process.
    /// </summary>
    public void Customize(IFixture fixture)
    {
        fixture.Customize<CustomValidatorRequestContext>(composer => composer
            .With(o => o.RememberMeRequested, false)
            .With(o => o.TwoFactorRecoveryRequested, false)
            .With(o => o.SsoRequired, false)
            .With(o => o.ValidationErrorResult, () => null)
            .With(o => o.CustomResponse, () => null));
    }
}

public class CustomValidatorRequestContextAttribute : CustomizeAttribute
{
    public CustomValidatorRequestContextAttribute()
    {
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        return new CustomValidatorRequestContextCustomization();
    }
}
