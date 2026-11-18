#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Request.Accounts;
using Bit.Identity.Models.Response.Accounts;
using Bit.SharedWeb.Utilities;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bit.Identity.Controllers;

/// <summary>
/// An internal lookup for a service of another platform that shares this install's people: whether an
/// address has an account here, and whether an organization here blocks account creation on the
/// address's domain. It lets that platform refuse a registration this install would refuse, in the
/// same order and words, without either platform reading the other's accounts.
/// </summary>
/// <remarks>
/// <para>
/// Only for a token this Identity issued to a <c>lookup.{service}</c> client
/// (<see cref="AccountLookupClientProvider"/>) for <see cref="ApiScopes.InternalAccountLookup"/>;
/// anything else is <c>401</c>. When <see cref="GlobalSettings.AccountLookupClientSecret"/> is unset
/// there are no such clients and the route is <c>404</c>. It is never meant to be reachable from
/// outside the install's network: the proxy in front of Identity should refuse <c>/internal/</c>.
/// </para>
/// <para>
/// It answers two booleans, the identifier of the account the address finds, as this install's database compares
/// addresses (its collation can find an account for a spelling another platform's comparison doesn't), and when that
/// account was made, and nothing else of any account. It logs neither the address nor the answer.
/// </para>
/// </remarks>
[Route("internal/account-lookup")]
// A trusted service's endpoint, not a client's: kept out of the OpenAPI the SDK is generated from.
[ApiExplorerSettings(IgnoreApi = true)]
[ExceptionHandlerFilter]
public class AccountLookupController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly ITokenValidator _tokenValidator;
    private readonly GlobalSettings _globalSettings;

    public AccountLookupController(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationDomainRepository organizationDomainRepository,
        ITokenValidator tokenValidator,
        GlobalSettings globalSettings)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _organizationDomainRepository = organizationDomainRepository;
        _tokenValidator = tokenValidator;
        _globalSettings = globalSettings;
    }

    /// <summary>Look up an address</summary>
    /// <remarks>
    /// <c>accountExists</c>: an account signs in with the address (this server's <c>UserManager</c> refuses a
    /// registration for it, "Email '…' is already taken."), and <c>accountId</c> is that account's
    /// identifier, the one sign-in and every other lookup by address finds. <c>accountCreationBlocked</c>: an
    /// organization other than <c>excludeOrganizationId</c> has verified the address's domain and
    /// blocks account creation on it (<c>OrganizationDomain_HasVerifiedDomainWithBlockPolicy</c>, the
    /// registration's "This email address is claimed by an organization using Bitwarden.").
    /// <c>accountCreationDate</c>: when the account with the address was made (UTC), <c>null</c> without one; the
    /// other platform, which may have given the address up, sends a sign-in to the newer account.
    /// </remarks>
    [HttpPost("")]
    [ProducesResponseType(typeof(AccountLookupResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostLookupAsync([FromBody] AccountLookupRequestModel model)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.AccountLookupClientSecret))
        {
            return NotFound();
        }

        if (!await AuthorizedAsync())
        {
            return Unauthorized();
        }

        var email = model.Email.Trim();
        var account = await _userRepository.GetByEmailAsync(email);
        // An address MailAddress can't read is refused in this server's words, as ever, unless the database's collation finds
        // an account for it (a full-width '@', say): that account is named, and no organization can have verified
        // such a domain.
        var domain = account is null ? EmailValidation.GetDomain(email) : DomainOf(email);
        var accountCreationBlocked = domain is not null && await _organizationDomainRepository
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(domain, model.ExcludeOrganizationId);

        // Stored in UTC; marked so, since a date read from the database carries no kind and would be written without one.
        var accountCreationDate = account is null ? (DateTime?)null : DateTime.SpecifyKind(account.CreationDate, DateTimeKind.Utc);

        return Ok(new AccountLookupResponseModel(account is not null, accountCreationBlocked, accountCreationDate, account?.Id));
    }

    private static string? DomainOf(string email)
    {
        try
        {
            return EmailValidation.GetDomain(email);
        }
        catch (BadRequestException)
        {
            return null;
        }
    }

    /// <summary>Look up an organization identifier</summary>
    /// <remarks>
    /// <c>organizationId</c>: the organization here whose single sign-on identifier this is
    /// (<c>Organization_ReadByIdentifier</c>, as <c>OrganizationService.UpdateAsync</c> reads it before
    /// refusing "Identifier already in use by another organization."), or <c>null</c>. The other
    /// platform refuses its own organization an identifier one here already uses, since the two share
    /// one namespace that signed-out single sign-on is routed by.
    /// </remarks>
    [HttpPost("organization-identifier")]
    [ProducesResponseType(typeof(OrganizationIdentifierLookupResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostOrganizationIdentifierLookupAsync([FromBody] OrganizationIdentifierLookupRequestModel model)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.AccountLookupClientSecret))
        {
            return NotFound();
        }

        if (!await AuthorizedAsync())
        {
            return Unauthorized();
        }

        var organization = await _organizationRepository.GetByIdentifierAsync(model.Identifier.Trim());
        return Ok(new OrganizationIdentifierLookupResponseModel(organization?.Id));
    }

    /// <summary>
    /// A bearer token this Identity issued, still valid, for the lookup's scope, to one of the
    /// lookup's clients.
    /// </summary>
    private async Task<bool> AuthorizedAsync()
    {
        var authorization = Request.Headers[HeaderNames.Authorization].ToString();
        const string bearer = "Bearer ";
        if (!authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorization[bearer.Length..].Trim();
        if (token.Length == 0)
        {
            return false;
        }

        var validated = await _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalAccountLookup);
        if (validated.IsError)
        {
            return false;
        }

        var client = validated.Claims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;
        return client is not null && client.StartsWith(AccountLookupClientProvider.Prefix + ".", StringComparison.Ordinal);
    }
}
