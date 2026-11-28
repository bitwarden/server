#nullable enable

using Bit.Core;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Response;
using Bit.SharedWeb.Utilities;
using Bitwarden.Server.Sdk.Features;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bit.Identity.Controllers;

/// <summary>
/// The Admin Portal's bulk cohort assignment upload (<c>OrganizationPlanMigrationCohortAssignmentsController</c>), for
/// an internal client: the same command, given the same file, and the command's answer in the command's order (the
/// page sorts line errors by line number; this does not).
/// </summary>
/// <remarks>
/// <para>
/// Only on the internal listener (<see cref="GlobalSettings.CohortAssignmentSyncPort"/>), and only for a token this
/// Identity issued to a <c>cohorts.{service}</c> client (<see cref="CohortAssignmentSyncClientProvider"/>) for
/// <see cref="ApiScopes.InternalCohortAssignmentSync"/>; a token for anything else is <c>401</c>. When
/// <see cref="GlobalSettings.CohortAssignmentSyncClientSecret"/> or the port is unset, the request came in on another
/// port, or the upload's feature flag is off (as the page answers), it is <c>404</c>. Never meant to be reachable from
/// outside the install's network: the proxy in front of Identity should refuse <c>/internal/</c> as well.
/// </para>
/// <para>
/// The command is all-or-nothing and changes nothing else. It logs the client's identifier and counts alone: never a
/// row of the file.
/// </para>
/// </remarks>
[Route("internal/staff/plan-migration-cohort-assignments")]
// For internal clients only, not Bitwarden's apps: kept out of the OpenAPI the SDK is generated from.
[ApiExplorerSettings(IgnoreApi = true)]
[ExceptionHandlerFilter]
public class CohortAssignmentSyncController : Controller
{
    private readonly IBulkSyncCohortAssignmentsCommand _bulkSyncCommand;
    private readonly IFeatureService _featureService;
    private readonly ITokenValidator _tokenValidator;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<CohortAssignmentSyncController> _logger;

    public CohortAssignmentSyncController(
        IBulkSyncCohortAssignmentsCommand bulkSyncCommand,
        IFeatureService featureService,
        ITokenValidator tokenValidator,
        GlobalSettings globalSettings,
        ILogger<CohortAssignmentSyncController> logger)
    {
        _bulkSyncCommand = bulkSyncCommand;
        _featureService = featureService;
        _tokenValidator = tokenValidator;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    /// <summary>Synchronize cohort assignments from a CSV file</summary>
    /// <remarks>
    /// The page's upload: <c>File</c>, a CSV of <c>OrganizationId,CohortName</c> with a header row, at most 25 MB,
    /// given to <see cref="IBulkSyncCohortAssignmentsCommand"/> unchanged. <c>200</c> with the summary, or with every
    /// line's error and nothing written, or with the command's refusal message; <c>400</c> for no file or one too big,
    /// in the page's words.
    /// </remarks>
    [HttpPost("bulk-sync")]
    [RequestSizeLimit(Constants.FileSize25mb + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = Constants.FileSize25mb + 1024 * 1024)]
    [ProducesResponseType(typeof(CohortAssignmentSyncResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkSyncAsync(IFormFile? file)
    {
        var (refusal, client) = await AuthorizeAsync();
        if (refusal is not null)
        {
            return refusal;
        }

        // The page's model: [Required(ErrorMessage = "Choose a CSV file to upload.")] and [MaxFileSize(25 MB)].
        if (file is null)
        {
            return BadRequest(new ErrorResponseModel("Choose a CSV file to upload."));
        }

        if (file.Length > Constants.FileSize25mb)
        {
            return BadRequest(new ErrorResponseModel($"CSV file exceeds the {Constants.FileSize25mb / (1024 * 1024)} MB limit."));
        }

        var result = await _bulkSyncCommand.Run(file);

        return result.Match<IActionResult>(
            value =>
            {
                _logger.LogInformation(
                    "Internal client {ClientId}'s cohort assignment sync {Outcome} with {ErrorCount} errors.",
                    client, value.Succeeded ? "applied" : "was refused", value.Errors.Count);
                return Ok(new CohortAssignmentSyncResponseModel(value));
            },
            badRequest => Refused(client, badRequest.Response),
            conflict => Refused(client, conflict.Response),
            unhandled => Refused(client, unhandled.Response));
    }

    private OkObjectResult Refused(string? client, string message)
    {
        _logger.LogInformation("Internal client {ClientId}'s cohort assignment sync was refused by the command.", client);
        return Ok(new CohortAssignmentSyncResponseModel(message));
    }

    /// <summary>
    /// <c>404</c> unless the sync is configured, the request came in on its internal listener and the upload's flag is
    /// on; <c>401</c> unless it carries a token for the sync's scope issued to one of its clients. Otherwise no
    /// refusal, and the client.
    /// </summary>
    private async Task<(IActionResult? Refusal, string? Client)> AuthorizeAsync()
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.CohortAssignmentSyncClientSecret)
            || _globalSettings.CohortAssignmentSyncPort is not { } port
            || HttpContext.Connection.LocalPort != port)
        {
            return (NotFound(), null);
        }

        var client = await AuthorizedClientAsync();
        if (client is null)
        {
            return (Unauthorized(), null);
        }

        // As the page checks it, after its sign-in.
        return _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            ? (null, client)
            : (NotFound(), null);
    }

    /// <summary>
    /// The client a bearer token this Identity issued, still valid, for the sync's scope, names, when it is one of the
    /// sync's clients; otherwise null.
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

        var validated = await _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalCohortAssignmentSync);
        if (validated.IsError)
        {
            return null;
        }

        var client = validated.Claims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;
        return client is not null && client.StartsWith(CohortAssignmentSyncClientProvider.Prefix + ".", StringComparison.Ordinal)
            ? client
            : null;
    }
}
