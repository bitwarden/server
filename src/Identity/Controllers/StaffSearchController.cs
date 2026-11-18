#nullable enable

using System.Net;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.HttpExtensions;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Response.StaffSearch;
using Bit.SharedWeb.Utilities;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bit.Identity.Controllers;

/// <summary>
/// An internal staff search for a service of another platform that shares this install's people: the
/// organizations, providers and users the Admin Portal's index pages would list for the same search, and
/// the organizations its add-existing-organization list would offer a reseller, found by the same repository
/// calls with the same arguments, and only what those pages show of each.
/// </summary>
/// <remarks>
/// <para>
/// Only on the internal listener (<see cref="GlobalSettings.StaffSearchPort"/>), and only for a token
/// this Identity issued to a <c>search.{service}</c> client (<see cref="StaffSearchClientProvider"/>)
/// for <see cref="ApiScopes.InternalStaffSearch"/>; a token for anything else is <c>401</c>. When
/// <see cref="GlobalSettings.StaffSearchClientSecret"/> or the port is unset, or the request came in on
/// another port, the routes are <c>404</c>. They are never meant to be reachable from outside the
/// install's network: the proxy in front of Identity should refuse <c>/internal/</c> as well.
/// </para>
/// <para>
/// Read-only. No keys, hashes, secrets or gateway identifiers are answered. It logs the client's
/// identifier and the number of results alone: never a search term, name or address.
/// </para>
/// </remarks>
[Route("internal/staff-search")]
// A trusted service's endpoint, not a client's: kept out of the OpenAPI the SDK is generated from.
[ApiExplorerSettings(IgnoreApi = true)]
[ExceptionHandlerFilter]
public class StaffSearchController : Controller
{
    /// <summary>The most results one request may ask for.</summary>
    public const int MaxTake = 100;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery;
    private readonly ITokenValidator _tokenValidator;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<StaffSearchController> _logger;

    public StaffSearchController(
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IUserRepository userRepository,
        ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
        ITokenValidator tokenValidator,
        GlobalSettings globalSettings,
        ILogger<StaffSearchController> logger)
    {
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
        _userRepository = userRepository;
        _twoFactorIsEnabledQuery = twoFactorIsEnabledQuery;
        _tokenValidator = tokenValidator;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    /// <summary>Search organizations</summary>
    /// <remarks>
    /// As the Admin Portal's organizations index searches: <c>name</c> (part of the name), <c>userEmail</c>
    /// (a member's exact address) and <c>paid</c> (whether a gateway subscription exists), newest first.
    /// An empty value is no filter.
    /// </remarks>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(ListResponseModel<StaffSearchOrganizationResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchOrganizationsAsync(
        [FromQuery] string? name = null,
        [FromQuery] string? userEmail = null,
        [FromQuery] bool? paid = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var (refusal, client) = await AuthorizeAsync();
        if (refusal is not null)
        {
            return refusal;
        }

        CheckPage(skip, take);

        // As the index encodes it: names are stored HTML-encoded. The repository takes null as no filter,
        // as the index (nullable references off) passes it.
        var encodedName = WebUtility.HtmlEncode(Absent(name));
        var organizations = await _organizationRepository.SearchAsync(encodedName!, Absent(userEmail)!, paid, skip, take);

        _logger.LogInformation(
            "Internal client {ClientId}'s staff search of organizations found {Count}.", client, organizations.Count);
        return Ok(new ListResponseModel<StaffSearchOrganizationResponseModel>(
            organizations.Select(organization => new StaffSearchOrganizationResponseModel(organization))));
    }

    /// <summary>Search organizations under no provider</summary>
    /// <remarks>
    /// As the Admin Portal's add-existing-organization list searches (<c>ProvidersController.AddExistingOrganization</c>,
    /// <see cref="IOrganizationRepository.SearchUnassignedToProviderAsync"/>): <c>name</c> (part of the name) and
    /// <c>ownerEmail</c> (the start of a member's address, as the list's procedure matches it), newest first, never a
    /// Free, Custom or Families organization. An empty value is no filter.
    /// </remarks>
    [HttpGet("unassigned-organizations")]
    [ProducesResponseType(typeof(ListResponseModel<StaffSearchUnassignedOrganizationResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchUnassignedOrganizationsAsync(
        [FromQuery] string? name = null,
        [FromQuery] string? ownerEmail = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var (refusal, client) = await AuthorizeAsync();
        if (refusal is not null)
        {
            return refusal;
        }

        CheckPage(skip, take);

        // As the list encodes it: names are stored HTML-encoded. The repository takes null as no filter, as the list
        // (nullable references off) passes it.
        var encodedName = WebUtility.HtmlEncode(Absent(name));
        var organizations = await _organizationRepository.SearchUnassignedToProviderAsync(
            encodedName!, Absent(ownerEmail)!, skip, take);

        _logger.LogInformation(
            "Internal client {ClientId}'s staff search of unassigned organizations found {Count}.", client, organizations.Count);
        return Ok(new ListResponseModel<StaffSearchUnassignedOrganizationResponseModel>(
            organizations.Select(organization => new StaffSearchUnassignedOrganizationResponseModel(organization))));
    }

    /// <summary>Search providers</summary>
    /// <remarks>
    /// As the Admin Portal's providers index searches: <c>name</c> (part of the name) and
    /// <c>userEmail</c> (a member's exact address), newest first. An empty value is no filter.
    /// </remarks>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(ListResponseModel<StaffSearchProviderResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchProvidersAsync(
        [FromQuery] string? name = null,
        [FromQuery] string? userEmail = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var (refusal, client) = await AuthorizeAsync();
        if (refusal is not null)
        {
            return refusal;
        }

        CheckPage(skip, take);

        // The repository takes null as no filter, as the index (nullable references off) passes it.
        var providers = await _providerRepository.SearchAsync(Absent(name)!, Absent(userEmail)!, skip, take);

        _logger.LogInformation(
            "Internal client {ClientId}'s staff search of providers found {Count}.", client, providers.Count);
        return Ok(new ListResponseModel<StaffSearchProviderResponseModel>(
            providers.Select(provider => new StaffSearchProviderResponseModel(provider))));
    }

    /// <summary>Search users</summary>
    /// <remarks>
    /// As the Admin Portal's users index searches: addresses beginning with <c>email</c>, in address
    /// order. <c>email</c> is required, so nobody may page through every account.
    /// </remarks>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ListResponseModel<StaffSearchUserResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchUsersAsync(
        [FromQuery] string? email = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var (refusal, client) = await AuthorizeAsync();
        if (refusal is not null)
        {
            return refusal;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BadRequestException("email is required.");
        }

        CheckPage(skip, take);

        var users = await _userRepository.SearchAsync(LiteralPrefix(email, _globalSettings.DatabaseProvider), skip, take);
        var twoFactorIsEnabled = (await _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(users.Select(user => user.Id)))
            .ToList();

        _logger.LogInformation("Internal client {ClientId}'s staff search of users found {Count}.", client, users.Count);
        return Ok(new ListResponseModel<StaffSearchUserResponseModel>(users.Select(user =>
            new StaffSearchUserResponseModel(
                user,
                twoFactorIsEnabled.FirstOrDefault(each => each.userId == user.Id).twoFactorIsEnabled))));
    }

    /// <summary>
    /// The address as a literal prefix for <see cref="IUserRepository.SearchAsync"/>, which matches
    /// <c>LIKE email%</c>: unescaped, <c>%</c> or <c>_</c> would match every account, where a staff
    /// search must name one. SQL Server's procedure has no
    /// <c>ESCAPE</c> clause, so brackets; Postgres's <c>ILIKE</c> escapes with a backslash; the other
    /// providers' <c>StartsWith</c> escapes for itself.
    /// </summary>
    internal static string LiteralPrefix(string email, string? databaseProvider) =>
        (databaseProvider ?? string.Empty).ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => email.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_"),
            "mysql" or "mariadb" or "sqlite" => email,
            _ => email.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]"),
        };

    /// <summary>
    /// <c>404</c> unless the search is configured and the request came in on its internal listener;
    /// <c>401</c> unless it carries a token for the search's scope issued to one of its clients.
    /// Otherwise no refusal, and the client.
    /// </summary>
    private async Task<(IActionResult? Refusal, string? Client)> AuthorizeAsync()
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.StaffSearchClientSecret)
            || _globalSettings.StaffSearchPort is not { } port
            || HttpContext.Connection.LocalPort != port)
        {
            return (NotFound(), null);
        }

        var client = await AuthorizedClientAsync();
        return client is null ? (Unauthorized(), null) : (null, client);
    }

    /// <summary>
    /// The client a bearer token this Identity issued, still valid, for the search's scope, names,
    /// when it is one of the search's clients; otherwise null.
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

        var validated = await _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalStaffSearch);
        if (validated.IsError)
        {
            return null;
        }

        var client = validated.Claims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;
        return client is not null && client.StartsWith(StaffSearchClientProvider.Prefix + ".", StringComparison.Ordinal)
            ? client
            : null;
    }

    private static void CheckPage(int skip, int take)
    {
        if (skip < 0)
        {
            throw new BadRequestException("skip must be 0 or more.");
        }

        if (take is < 1 or > MaxTake)
        {
            throw new BadRequestException($"take must be from 1 to {MaxTake}.");
        }
    }

    /// <summary>An empty value is no filter, as the index's form leaves it.</summary>
    private static string? Absent(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
