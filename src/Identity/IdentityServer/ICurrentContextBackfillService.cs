using System.Security.Claims;
using Bit.Core.Context;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer;

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
