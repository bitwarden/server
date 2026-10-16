using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class CreatePartnershipActivationLinkCommand(
    IOrganizationPartnershipEntitlementRepository organizationPartnershipEntitlementRepository,
    IDataProtectorTokenFactory<PartnershipActivationTokenable> tokenFactory,
    TimeProvider timeProvider,
    GlobalSettings globalSettings)
    : ICreatePartnershipActivationLinkCommand
{
    /// <summary>
    /// The web vault route that receives an activation link, with the token in the <see cref="TokenQueryParameter"/>
    /// query parameter. PM-42544 owns the landing route and single-use enforcement of the token.
    /// </summary>
    public const string ActivationLandingPath = "/#/partner-activation";
    public const string TokenQueryParameter = "token";

    public async Task<CommandResult<PartnershipActivationLink>> CreateAsync(CreatePartnershipActivationLinkRequest request)
    {
        if (!PartnershipEntitlementRules.IsValidExternalId(request.ExternalId))
        {
            return new InvalidExternalId();
        }

        string? returnUrl = null;
        if (request.ReturnUrl is not null && !TryValidateReturnUrl(request.Partnership, request.ReturnUrl, out returnUrl))
        {
            return new InvalidReturnUrl();
        }

        if (request.Partnership.Status != PartnershipStatus.Active)
        {
            return new PartnershipNotActive();
        }

        var entitlement = await organizationPartnershipEntitlementRepository
            .GetByExternalIdAsync(request.Partnership.Id, request.ExternalId);
        if (entitlement is null)
        {
            return new EntitlementNotFound();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var bindableError = ValidateBindable(entitlement, now);
        if (bindableError is not null)
        {
            return bindableError;
        }

        var tokenable = new PartnershipActivationTokenable(request.Partnership.Id, entitlement.Id, returnUrl, now);
        var token = tokenFactory.Protect(tokenable);
        var activationUrl =
            $"{globalSettings.BaseServiceUri.Vault.TrimEnd('/')}{ActivationLandingPath}?{TokenQueryParameter}={Uri.EscapeDataString(token)}";

        return new PartnershipActivationLink(activationUrl, token, tokenable.ExpirationDate);
    }

    private static Error? ValidateBindable(OrganizationPartnershipEntitlement entitlement, DateTime now) =>
        entitlement.State switch
        {
            PartnershipEntitlementState.Provisioned => null,
            PartnershipEntitlementState.Canceled when entitlement.UserId is null => new EntitlementNotProvisioned(),
            PartnershipEntitlementState.Canceled when !PartnershipEntitlementRules.IsWithinResumeWindow(entitlement, now) =>
                new EntitlementNotResumable(),
            PartnershipEntitlementState.Canceled => null,
            _ => new EntitlementAlreadyBound(),
        };

    /// <summary>
    /// Accepts an absolute https URL without user info whose origin exactly matches a registered origin. Registered
    /// origins are stored in the same normalized form, so the comparison only needs to ignore case.
    /// </summary>
    private static bool TryValidateReturnUrl(OrganizationPartnership partnership, string returnUrl, out string? normalized)
    {
        normalized = null;
        if (returnUrl.Length > CreatePartnershipActivationLinkRequest.ReturnUrlMaxLength ||
            !Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (!partnership.GetRegisteredReturnOrigins().Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }
}
