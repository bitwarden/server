#nullable enable

using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
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
/// An internal retirement for a service of another platform that shares this install's tenants: deletes this install's
/// rows that start its recurring jobs for users and organizations, by their identifiers, once that platform serves them.
/// What this install keeps of them stays, but its jobs no longer act on it.
/// </summary>
/// <remarks>
/// <para>
/// Only on the internal listener (<see cref="GlobalSettings.JobTriggerRetirementPort"/>), and only for a token this
/// Identity issued to a <c>retire.{service}</c> client (<see cref="JobTriggerRetirementClientProvider"/>) for
/// <see cref="ApiScopes.InternalJobTriggerRetirement"/>; a token for anything else is <c>401</c>. When
/// <see cref="GlobalSettings.JobTriggerRetirementClientSecret"/> or the port is unset, or the request came in on another
/// port, the route is <c>404</c>. It is never meant to be reachable from outside the install's network: the proxy in
/// front of Identity should refuse <c>/internal/</c> as well.
/// </para>
/// <para>
/// It deletes with the repositories directly, not with the commands people use: those tell the people involved and
/// record events, and nothing here is anybody's doing. It reads only identifiers, returns only counts and logs only
/// the counts and the client's identifier. Safe to repeat: a second request finds nothing left to delete.
/// </para>
/// </remarks>
[Route("internal/job-triggers")]
// A trusted service's endpoint, not a client's: kept out of the OpenAPI the SDK is generated from.
[ApiExplorerSettings(IgnoreApi = true)]
[ExceptionHandlerFilter]
public class JobTriggerRetirementController : Controller
{
    /// <summary>The most identifiers, users' and organizations' together, one request may name.</summary>
    public const int MaxIds = 1000;

    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _cohortAssignmentRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITokenValidator _tokenValidator;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<JobTriggerRetirementController> _logger;

    public JobTriggerRetirementController(
        IEmergencyAccessRepository emergencyAccessRepository,
        IOrganizationDomainRepository organizationDomainRepository,
        IOrganizationPlanMigrationCohortAssignmentRepository cohortAssignmentRepository,
        IOrganizationRepository organizationRepository,
        ITokenValidator tokenValidator,
        GlobalSettings globalSettings,
        ILogger<JobTriggerRetirementController> logger)
    {
        _emergencyAccessRepository = emergencyAccessRepository;
        _organizationDomainRepository = organizationDomainRepository;
        _cohortAssignmentRepository = cohortAssignmentRepository;
        _organizationRepository = organizationRepository;
        _tokenValidator = tokenValidator;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    /// <summary>Retire users' and organizations' job triggers</summary>
    /// <remarks>
    /// Deletes every emergency access grant a user is grantor or grantee of (the automatic approval and the reminder
    /// emails), and every organization's domain claims (the verification job), price migration cohort assignment (the
    /// migration's scheduling, emails and payment calls), and clears its pending seat sync. <c>400</c> for more than
    /// <see cref="MaxIds"/> identifiers, with nothing deleted.
    /// </remarks>
    [HttpPost("retire")]
    [ProducesResponseType(typeof(JobTriggerRetirementResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetireAsync([FromBody] JobTriggerRetirementRequestModel? model)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.JobTriggerRetirementClientSecret)
            || _globalSettings.JobTriggerRetirementPort is not { } port
            || HttpContext.Connection.LocalPort != port)
        {
            return NotFound();
        }

        var client = await AuthorizedClientAsync();
        if (client is null)
        {
            return Unauthorized();
        }

        if (model is null)
        {
            throw new BadRequestException("Name the users and organizations whose job triggers to retire.");
        }

        var userIds = model.UserIds ?? [];
        var organizationIds = model.OrganizationIds ?? [];
        if (userIds.Count + organizationIds.Count > MaxIds)
        {
            throw new BadRequestException($"At most {MaxIds} users and organizations at a time.");
        }

        var emergencyAccessDeleted = await DeleteEmergencyAccessAsync(userIds.Distinct().ToList());

        var distinctOrganizationIds = organizationIds.Distinct().ToList();
        var domainClaimsDeleted = 0;
        var cohortAssignmentsDeleted = 0;
        foreach (var organizationId in distinctOrganizationIds)
        {
            foreach (var domain in await _organizationDomainRepository.GetDomainsByOrganizationIdAsync(organizationId))
            {
                await _organizationDomainRepository.DeleteAsync(domain);
                domainClaimsDeleted++;
            }

            var assignment = await _cohortAssignmentRepository.GetByOrganizationIdAsync(organizationId);
            if (assignment is not null)
            {
                await _cohortAssignmentRepository.DeleteAsync(assignment);
                cohortAssignmentsDeleted++;
            }
        }

        if (distinctOrganizationIds.Count > 0)
        {
            await _organizationRepository.UpdateSuccessfulOrganizationSyncStatusAsync(distinctOrganizationIds, DateTime.UtcNow);
        }

        _logger.LogInformation(
            "Retired job triggers at internal client {ClientId}'s request: {EmergencyAccessDeleted} emergency access grants, "
            + "{DomainClaimsDeleted} domain claims and {CohortAssignmentsDeleted} cohort assignments deleted, "
            + "{OrganizationsSeatSyncCleared} organizations' seat sync cleared.",
            client, emergencyAccessDeleted, domainClaimsDeleted, cohortAssignmentsDeleted, distinctOrganizationIds.Count);

        return Ok(new JobTriggerRetirementResponseModel(
            emergencyAccessDeleted, domainClaimsDeleted, cohortAssignmentsDeleted, distinctOrganizationIds.Count));
    }

    /// <summary>
    /// Deletes every emergency access grant the users are grantor or grantee of, and answers how many. Only the grants'
    /// identifiers are used: nothing else of them, their keys least of all, is read.
    /// </summary>
    private async Task<int> DeleteEmergencyAccessAsync(ICollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return 0;
        }

        var grantIds = (await _emergencyAccessRepository.GetManyDetailsByUserIdsAsync(userIds))
            .Select(grant => grant.Id)
            .Distinct()
            .ToList();
        if (grantIds.Count == 0)
        {
            return 0;
        }

        await _emergencyAccessRepository.DeleteManyAsync(grantIds);
        return grantIds.Count;
    }

    /// <summary>
    /// The client a bearer token this Identity issued, still valid, for the retirement's scope, names, when it is one of
    /// the retirement's clients; otherwise null.
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

        var validated = await _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalJobTriggerRetirement);
        if (validated.IsError)
        {
            return null;
        }

        var client = validated.Claims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;
        return client is not null && client.StartsWith(JobTriggerRetirementClientProvider.Prefix + ".", StringComparison.Ordinal)
            ? client
            : null;
    }
}
