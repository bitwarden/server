using System.Security.Claims;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

/// <summary>
/// Back-fills <see cref="ICurrentContext"/> from grant-validator state that
/// <c>CurrentContextMiddleware</c> can't see for <c>/connect/token</c>:
/// the form-body <c>DeviceIdentifier</c>, the validator-resolved user/device,
/// and (for refresh / authorization_code) the validated-request subject. Uses
/// <c>??=</c> so anything the middleware already populated wins.
/// </summary>
/// <remarks>
/// Call from every grant validator entry point so feature flag evaluations
/// anywhere downstream get a reliable LaunchDarkly context (device-keyed and
/// user-keyed targeting rules need these populated).
///
/// Best-effort: failures never propagate. Token refresh / login must not break
/// because flag bucketing context couldn't be assembled.
/// </remarks>
public interface ICurrentContextBackfillService
{
    void Apply(
        ICurrentContext currentContext,
        ValidatedRequest? request,
        ClaimsPrincipal? subject = null,
        CustomValidatorRequestContext? validatorContext = null);
}

public class CurrentContextBackfillService(ILogger<CurrentContextBackfillService> logger) : ICurrentContextBackfillService
{
    public void Apply(
        ICurrentContext currentContext,
        ValidatedRequest? request,
        ClaimsPrincipal? subject = null,
        CustomValidatorRequestContext? validatorContext = null)
    {
        try
        {
            ApplyCore(currentContext, request, subject, validatorContext);
        }
        catch (Exception e)
        {
            // Best-effort back-fill — never fail a token refresh or login because we
            // couldn't assemble flag bucketing context. The downstream flag eval will
            // simply use whatever CurrentContext had on entry (usually anonymous),
            // which buckets as a miss for device/user-keyed rollouts.
            logger.LogWarning(e,
                "Failed to back-fill CurrentContext for token request. Continuing; " +
                "feature flag bucketing may fall back to anonymous context.");
        }
    }

    private static void ApplyCore(
        ICurrentContext currentContext,
        ValidatedRequest? request,
        ClaimsPrincipal? subject,
        CustomValidatorRequestContext? validatorContext)
    {
        // Subject path — populated for grants that carry an existing principal:
        //   refresh_token:      subject is the refresh-token principal; carries `sub` and `device` claims.
        //   authorization_code: subject is the auth-code principal; carries `sub`, may carry `device`.
        // Not populated for password / webauthn / client_credentials at this stage.
        if (subject is not null)
        {
            // Read `sub` directly via FindFirstValue rather than Duende's GetSubjectId()
            // extension — the latter THROWS when the claim is missing, which is too
            // aggressive for back-fill semantics (we want a safe no-op on malformed
            // or incomplete principals).
            if (currentContext.UserId is null &&
                Guid.TryParse(subject.FindFirstValue(JwtClaimTypes.Subject), out var subjectUserId))
            {
                currentContext.UserId = subjectUserId;
            }
            var subjectDevice = NullIfBlank(subject.FindFirstValue(Claims.Device));
            if (subjectDevice is not null)
            {
                currentContext.DeviceIdentifier ??= subjectDevice;
            }
        }

        // ValidatorContext.User path — populated by the derived grant validator before
        // it calls base.ValidateAsync:
        //   password: ResourceOwnerPasswordValidator looks up the user by email.
        //   webauthn: WebAuthnGrantValidator resolves the user from the assertion.
        // Empty for refresh (base.ValidateAsync not called) and for the initial entry to
        // CustomTokenRequestValidator (which passes an empty CustomValidatorRequestContext).
        if (validatorContext?.User?.Id is Guid validatorUserId)
        {
            currentContext.UserId ??= validatorUserId;
        }

        // DeviceIdentifier fallback chain:
        //   validatorContext.Device:  password (DeviceValidator built it on the way in).
        //   request.Raw form body:    password / webauthn entry preludes — runs before
        //                             DeviceValidator and is the form-body the web client
        //                             always sends. Not present on refresh / authorization_code
        //                             / client_credentials.
        // Empty/whitespace string sources are normalized to null so a future back-fill
        // attempt isn't blocked by a non-null placeholder (e.g., a form body sending
        // `DeviceIdentifier=` with no value).
        var device = NullIfBlank(validatorContext?.Device?.Identifier)
            ?? NullIfBlank(request?.Raw?["DeviceIdentifier"]);
        if (device is not null)
        {
            currentContext.DeviceIdentifier ??= device;
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
