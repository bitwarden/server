using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases/ciphers/{id}</c> resource: the per-cipher leasing entry points (pre-check, state,
/// submit). The Minimal API endpoints (see <c>CipherLeaseEndpoints</c>) resolve this handler from DI. The deprecated
/// full-cipher read-back (<c>GET …/cipher</c>) is hosted separately, by a small MVC controller in the Api project,
/// since it depends on the Api Vault response models.
/// </summary>
/// <remarks>
/// The automatic request→activate flow is the only path wired in this build (see <c>SubmitAccessRequestCommand</c>);
/// human approval and the approver/governance surfaces land with a later slice.
/// </remarks>
public class CipherLeaseEndpointsHandler(
    ICurrentContext currentContext,
    ICipherRepository cipherRepository,
    IGoverningRuleResolver resolver,
    IAccessLeaseRepository accessLeaseRepository,
    IAccessRequestRepository accessRequestRepository,
    ISubmitAccessRequestCommand submitAccessRequestCommand,
    TimeProvider timeProvider)
{
    public async Task<AccessPreCheckResponseModel> PreCheck(ClaimsPrincipal user, Guid id)
    {
        var userId = currentContext.UserId!.Value;

        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        var cipher = await cipherRepository.GetByIdAsync(id, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // A caller who already holds an active lease should be sent straight to the credential, not prompted to
        // make a request that SubmitAsync would reject. This mirrors the active-lease guard there.
        if (await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, id, now) is not null)
        {
            return new AccessPreCheckResponseModel
            {
                CipherId = id,
                ApprovalMode = AccessApprovalMode.Automatic,
                HasActiveLease = true,
            };
        }

        var signals = AccessSignals.From(currentContext.IpAddress);
        var governingRule = await resolver.ResolveAsync(userId, id, signals);
        // A cipher with no governing rule is not leasing-gated for this caller — report it the same as a rule with
        // no human-approval gate, so the client's default flow (pick a duration) still applies.
        var approvalMode = governingRule?.RequiresHumanApproval == true
            ? AccessApprovalMode.Human
            : AccessApprovalMode.Automatic;

        return new AccessPreCheckResponseModel
        {
            CipherId = id,
            ApprovalMode = approvalMode,
            HasActiveLease = false,
        };
    }

    public async Task<CipherAccessStateResponseModel> State(ClaimsPrincipal user, Guid id)
    {
        var userId = currentContext.UserId!.Value;

        var cipher = await cipherRepository.GetByIdAsync(id, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activeLease = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, id, now);
        var pending = await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, id);
        var approved = await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(userId, id, now);

        var extensionsAllowed = false;
        int? maxExtensionDurationSeconds = null;
        if (activeLease is not null)
        {
            // Extension eligibility drives the banner's "Extend" control. A lease may be extended once, so it is
            // extendable only while the rule opts in and no extension has been recorded yet; surface the rule's max
            // length so the client can cap its duration picker.
            var signals = AccessSignals.From(currentContext.IpAddress);
            var rule = await resolver.ResolveAsync(userId, id, signals);
            if (rule?.AllowsExtensions == true)
            {
                var used = await accessRequestRepository.CountExtensionsByLeaseIdAsync(activeLease.Id);
                extensionsAllowed = used == 0;
                maxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds;
            }
        }
        else if (pending is null && approved is null)
        {
            var signals = AccessSignals.From(currentContext.IpAddress);
            if (await resolver.ResolveAsync(userId, id, signals) is null)
            {
                // Nothing to report and the cipher isn't leasing-gated. (When a lease or request exists we still
                // return a snapshot even if the rule was since removed, so the caller's state isn't hidden.)
                throw new NotFoundException();
            }
        }

        return new CipherAccessStateResponseModel
        {
            CipherId = id,
            ActiveLease = activeLease is null ? null : new AccessLeaseResponseModel(activeLease),
            PendingRequest = pending is null ? null : new AccessRequestDetailsResponseModel(pending),
            ApprovedRequest = approved is null ? null : new AccessRequestDetailsResponseModel(approved),
            ExtensionsAllowed = extensionsAllowed,
            MaxExtensionDurationSeconds = maxExtensionDurationSeconds,
        };
    }

    public async Task<AccessRequestResultResponseModel> Post(ClaimsPrincipal user, Guid id, AccessRequestCreateRequestModel model)
    {
        var userId = currentContext.UserId!.Value;
        var request = await submitAccessRequestCommand.SubmitAsync(userId, id, model.DurationSeconds ?? 0, model.Reason);
        return new AccessRequestResultResponseModel(request);
    }
}
