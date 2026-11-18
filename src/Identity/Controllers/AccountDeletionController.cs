#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Response.Accounts;
using Bit.SharedWeb.Utilities;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bit.Identity.Controllers;

/// <summary>
/// An internal deletion for a client holding a <c>deletion.{service}</c> token: deletes one account, by its
/// identifier, with the account holder's own deletion
/// (<see cref="IUserService.DeleteAsync(Core.Entities.User)"/>). One difference, on purpose: the account's
/// payment gateway customer and subscription are forgotten first, so the deletion cancels nothing at the
/// payment gateway. The caller is responsible for the account's billing.
/// </summary>
/// <remarks>
/// <para>
/// Only on the internal listener (<see cref="GlobalSettings.AccountDeletionPort"/>), and only for a
/// token this Identity issued to a <c>deletion.{service}</c> client
/// (<see cref="AccountDeletionClientProvider"/>) for <see cref="ApiScopes.InternalAccountDeletion"/>;
/// a token for anything else is <c>401</c>. When <see cref="GlobalSettings.AccountDeletionClientSecret"/>
/// or the port is unset, or the request came in on another port, the route is <c>404</c>. It is never
/// meant to be reachable from outside the install's network: the proxy in front of Identity should refuse
/// <c>/internal/</c> as well.
/// </para>
/// <para>
/// Everything the holder's own deletion checks applies: an account an organization claims, the only
/// owner of an organization (unless it is the one free or disabled organization they alone belong to,
/// which goes with them) or of a provider is refused with that deletion's words, and nothing is deleted.
/// It reads and returns nothing of the account, and logs only its identifier and the client's.
/// </para>
/// </remarks>
[Route("internal/accounts")]
// A trusted service's endpoint, not a client's: kept out of the OpenAPI the SDK is generated from.
[ApiExplorerSettings(IgnoreApi = true)]
[ExceptionHandlerFilter]
public class AccountDeletionController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ITokenValidator _tokenValidator;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<AccountDeletionController> _logger;

    public AccountDeletionController(
        IUserRepository userRepository,
        IUserService userService,
        ITokenValidator tokenValidator,
        GlobalSettings globalSettings,
        ILogger<AccountDeletionController> logger)
    {
        _userRepository = userRepository;
        _userService = userService;
        _tokenValidator = tokenValidator;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    /// <summary>Delete an account</summary>
    /// <remarks>
    /// <c>deleted</c> is true when the account was deleted, and false when there is no account with
    /// the identifier (already deleted, or never created). A refusal is <c>400</c> in the holder's
    /// deletion's words, with nothing deleted.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AccountDeletionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.AccountDeletionClientSecret)
            || _globalSettings.AccountDeletionPort is not { } port
            || HttpContext.Connection.LocalPort != port)
        {
            return NotFound();
        }

        var client = await AuthorizedClientAsync();
        if (client is null)
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
        {
            return Ok(new AccountDeletionResponseModel(false));
        }

        // The caller is responsible for billing: the gateway customer and subscription are forgotten on the
        // account alone, so the deletion cancels nothing and leaves the payment gateway untouched.
        user.Gateway = null;
        user.GatewayCustomerId = null;
        user.GatewaySubscriptionId = null;

        // The holder's own deletion, with all of its checks: a claimed account throws, the other
        // refusals come back as the result's errors.
        var result = await _userService.DeleteAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogInformation(
                "Refused internal client {ClientId}'s deletion of user {UserId}.", client, id);
            throw new BadRequestException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        _logger.LogInformation("Deleted user {UserId} at internal client {ClientId}'s request.", id, client);
        return Ok(new AccountDeletionResponseModel(true));
    }

    /// <summary>
    /// The client a bearer token this Identity issued, still valid, for the deletion's scope, names,
    /// when it is one of the deletion's clients; otherwise null.
    /// </summary>
    private async Task<string?> AuthorizedClientAsync()
    {
        var authorization = Request.Headers[HeaderNames.Authorization].ToString();
        const string bearer = "Bearer ";
        if (!authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization[bearer.Length..].Trim();
        if (token.Length == 0)
        {
            return null;
        }

        var validated = await _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalAccountDeletion);
        if (validated.IsError)
        {
            return null;
        }

        var client = validated.Claims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;
        return client is not null && client.StartsWith(AccountDeletionClientProvider.Prefix + ".", StringComparison.Ordinal)
            ? client
            : null;
    }
}
