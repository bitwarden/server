using Bit.Core;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

/// <summary>
/// Validates whether a user is required to authenticate via SSO based on organization policies.
/// </summary>
public class SsoRequestValidator(
    IPolicyService policyService,
    IFeatureService featureService,
    IPolicyRequirementQuery policyRequirementQuery) : ISsoRequestValidator
{
    /// <summary>
    /// Validates the SSO requirement for a user attempting to authenticate.
    /// Sets context.SsoRequired to indicate whether SSO is required.
    /// If SSO is required, sets the validation error result and custom response in the context.
    /// </summary>
    /// <param name="user">The user attempting to authenticate.</param>
    /// <param name="request">The token request containing grant type and other authentication details.</param>
    /// <param name="context">The validator context to be updated with SSO requirement status and error results if applicable.</param>
    /// <returns>true if the user can proceed with authentication; false if SSO is required and the user must be redirected to SSO flow.</returns>
    public async Task<bool> ValidateAsync(User user, ValidatedTokenRequest request, CustomValidatorRequestContext context)
    {
        context.SsoRequired = await RequireSsoLoginAsync(user, request.GrantType);

        if (!context.SsoRequired)
        {
            return true;
        }

        // Users without SSO requirement requesting 2FA recovery will be fast-forwarded through login and are
        // presented with their 2FA management area as a reminder to re-evaluate their 2FA posture after recovery and
        // review their new recovery token if desired.
        // SSO users cannot be assumed to be authenticated, and must prove authentication with their IdP after recovery.
        // As described in validation order determination, if TwoFactorRequired, the 2FA validation scheme will have been
        // evaluated, and recovery will have been performed if requested.
        // We will send a descriptive message in these cases so clients can give the appropriate feedback and redirect
        // to /login.
        // If the feature flag RecoveryCodeSupportForSsoRequiredUsers is set to false then this code is unreachable since
        // Two Factor validation occurs after SSO validation in that scenario.
        if (context.TwoFactorRequired && context.TwoFactorRecoveryRequested)
        {
            SetContextCustomResponseSsoError(context, "Two-factor recovery has been performed. SSO authentication is required.");
            return false;
        }

        SetContextCustomResponseSsoError(context, "SSO authentication is required.");
        return false;
    }

    /// <summary>
    /// Check if the user is required to authenticate via SSO. If the user requires SSO, but they are
    /// logging in using an API Key (client_credentials) then they are allowed to bypass the SSO requirement.
    /// If the GrantType is authorization_code or client_credentials we know the user is trying to login
    /// using the SSO flow so they are allowed to continue.
    /// </summary>
    /// <param name="user">user trying to login</param>
    /// <param name="grantType">magic string identifying the grant type requested</param>
    /// <returns>true if sso required; false if not required or already in process</returns>
    private async Task<bool> RequireSsoLoginAsync(User user, string grantType)
    {
        if (grantType == OidcConstants.GrantTypes.AuthorizationCode ||
            grantType == OidcConstants.GrantTypes.ClientCredentials)
        {
            // SSO is not required for users already using SSO to authenticate, or logging-in via API key
            // allow user to continue request validation
            return false;
        }

        // Check if user belongs to any organization with an active SSO policy
        var ssoRequired = featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements)
            ? (await policyRequirementQuery.GetAsync<RequireSsoPolicyRequirement>(user.Id))
            .SsoRequired
            : await policyService.AnyPoliciesApplicableToUserAsync(
                user.Id, PolicyType.RequireSso, OrganizationUserStatusType.Confirmed);

        if (ssoRequired)
        {
            return true;
        }

        // Default - SSO is not required
        return false;
    }

    /// <summary>
    /// Sets the customResponse in the context with the error result for the SSO validation failure.
    /// </summary>
    /// <param name="context">The validator context to update with error details.</param>
    /// <param name="errorMessage">The error message to return to the client.</param>
    private static void SetContextCustomResponseSsoError(CustomValidatorRequestContext context, string errorMessage)
    {
        context.ValidationErrorResult = new ValidationResult
        {
            IsError = true,
            Error = "sso_required",
            ErrorDescription = errorMessage
        };

        context.CustomResponse = new Dictionary<string, object>
        {
            { "ErrorModel", new ErrorResponseModel(errorMessage) }
        };
    }
}
