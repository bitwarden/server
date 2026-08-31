using System.Globalization;
using Bit.Core;
using Bit.Core.Pam.Models.Mail.AccessRequestPending;
using Bit.Core.Pam.Models.Mail.AccessRequestsWaiting;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Services.Pam.Services;

public class ApproverMailNotifier : IApproverMailNotifier
{
    /// <summary>
    /// How many requests one approver is mailed individually inside <see cref="BurstWindowMinutes" /> before the
    /// rest of the window collapses into a single message. Five is a volume one person can still triage one mail
    /// at a time; a sixth arriving in the same quarter-hour is a burst — onboarding, an incident, a scripted
    /// submission — rather than organic demand.
    /// </summary>
    public const int BurstThreshold = 5;

    /// <summary>
    /// How long the breaker stays tripped. It bounds the damage in the direction that matters: after at most
    /// fifteen minutes an approver is back to receiving a request's own email, so a genuinely urgent request that
    /// lands behind a burst waits a quarter-hour at worst, and the push and the collapsed mail's inbox link cover
    /// that gap.
    /// </summary>
    public const int BurstWindowMinutes = 15;

    private static readonly TimeSpan _burstWindow = TimeSpan.FromMinutes(BurstWindowMinutes);

    private readonly IAccessMailNotifier _accessMailNotifier;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDistributedCache _cache;
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApproverMailNotifier> _logger;

    public ApproverMailNotifier(
        IAccessMailNotifier accessMailNotifier,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IDistributedCache cache,
        IGlobalSettings globalSettings,
        IFeatureService featureService,
        TimeProvider timeProvider,
        ILogger<ApproverMailNotifier> logger)
    {
        _accessMailNotifier = accessMailNotifier;
        _collectionRepository = collectionRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _cache = cache;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task NotifyPendingRequestAsync(AccessRequest request)
    {
        // Not merely an optimisation over the identical guard inside IAccessMailNotifier: the breaker's window is
        // claimed before the send, so running the body with the flag off would spend every approver's window on
        // mail that is then dropped, and the first requests after the flag is turned on would notify nobody.
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamEmailNotifications))
        {
            return;
        }

        try
        {
            // A requester may well manage the collection they are requesting against — an org Owner always does when
            // AllowAdminAccessToAllCollectionItems is on — but DecideAccessRequestCommand refuses a self-decision, so
            // mailing them would send the one person who already knows to an action the server rejects.
            var approverIds = (await _collectionRepository.GetManagingUserIdsAsync(request.CollectionId))
                .Where(id => id != request.RequesterId)
                .Distinct()
                .ToList();
            if (approverIds.Count == 0)
            {
                return;
            }

            var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId);
            var requester = await _userRepository.GetByIdAsync(request.RequesterId);
            if (organization is null || string.IsNullOrWhiteSpace(requester?.Email))
            {
                _logger.LogWarning(
                    "PAM pending-request mail for access request {AccessRequestId}: organization or requester could not be resolved; nothing sent.",
                    request.Id);
                return;
            }

            var perRequest = new List<Guid>();
            var collapsed = new List<Guid>();
            foreach (var approverId in approverIds)
            {
                switch (await ClaimAsync(approverId))
                {
                    case BurstDecision.SendRequest:
                        perRequest.Add(approverId);
                        break;
                    case BurstDecision.SendSummary:
                        collapsed.Add(approverId);
                        break;
                }
            }

            await SendPerRequestAsync(perRequest, request, organization.Name, requester.Email);
            await SendCollapsedAsync(collapsed);
        }
        catch (Exception ex)
        {
            // Ids only: neither the approvers' nor the requester's address, and never the request's reason.
            _logger.LogError(ex,
                "PAM pending-request mail for access request {AccessRequestId} could not be sent.", request.Id);
        }
    }

    private async Task SendPerRequestAsync(
        IReadOnlyCollection<Guid> approverIds, AccessRequest request, string organizationName, string requesterEmail)
    {
        if (approverIds.Count == 0)
        {
            return;
        }

        var view = new AccessRequestPendingView
        {
            WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
            AccessRequestId = request.Id,
            OrganizationName = organizationName,
            RequesterEmail = requesterEmail,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
        };

        await _accessMailNotifier.SendToUsersAsync(approverIds,
            email => new AccessRequestPendingMail { ToEmails = [email], View = view });
    }

    private async Task SendCollapsedAsync(IReadOnlyCollection<Guid> approverIds)
    {
        if (approverIds.Count == 0)
        {
            return;
        }

        // Every recipient here tripped the breaker on this same request, so their count is BurstThreshold + 1 by
        // construction and one shared view is correct. The count spans every organization the approver manages in,
        // which is why the view names none of them.
        var view = new AccessRequestsWaitingView
        {
            WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
            RequestCount = BurstThreshold + 1,
            WindowMinutes = BurstWindowMinutes,
        };

        await _accessMailNotifier.SendToUsersAsync(approverIds,
            email => new AccessRequestsWaitingMail { ToEmails = [email], View = view });
    }

    /// <summary>
    /// Records this request against <paramref name="approverId" />'s window and reports what they should be sent.
    /// The window is claimed before the mail goes out, not after: a burst is exactly the case where several
    /// submissions land at once, and claiming afterwards would let each of them read an empty window and fan out.
    /// </summary>
    private async Task<BurstDecision> ClaimAsync(Guid approverId)
    {
        var key = $"pam:approver-request-mail:{approverId}";
        var now = _timeProvider.GetUtcNow();

        DateTimeOffset windowStart;
        int count;
        try
        {
            var existing = Parse(await _cache.GetStringAsync(key));
            (windowStart, count) = existing is { } window && now - window.StartedAt < _burstWindow
                ? window
                : (now, 0);
        }
        catch (Exception ex)
        {
            // Fail open. The breaker is a courtesy to the approver's inbox; the notification is the feature. A cache
            // outage that silenced these mails would leave requesters blocked on approvers who were never told.
            _logger.LogWarning(ex, "PAM pending-request mail: burst window unreadable; sending unthrottled.");
            return BurstDecision.SendRequest;
        }

        var next = count + 1;
        if (next > BurstThreshold + 1)
        {
            // The collapsed mail for this window has already gone out. Returning early also caps the cache writes a
            // burst can generate at BurstThreshold + 1 per approver, when write load is highest.
            return BurstDecision.Suppress;
        }

        try
        {
            var entry = string.Create(CultureInfo.InvariantCulture, $"{windowStart.UtcTicks}:{next}");
            await _cache.SetStringAsync(key, entry, new DistributedCacheEntryOptions
            {
                // Absolute, derived from the window's own start, so re-writing the counter cannot slide the window
                // forward. A sliding expiry would leave a steadily busy collection's approvers suppressed forever.
                AbsoluteExpiration = windowStart + _burstWindow,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PAM pending-request mail: burst window could not be recorded; sending unthrottled.");
            return BurstDecision.SendRequest;
        }

        return next <= BurstThreshold ? BurstDecision.SendRequest : BurstDecision.SendSummary;
    }

    private static (DateTimeOffset StartedAt, int Count)? Parse(string? entry)
    {
        if (string.IsNullOrEmpty(entry))
        {
            return null;
        }

        var separator = entry.IndexOf(':');
        if (separator <= 0
            || !long.TryParse(entry[..separator], CultureInfo.InvariantCulture, out var ticks)
            || !int.TryParse(entry[(separator + 1)..], CultureInfo.InvariantCulture, out var count))
        {
            return null;
        }

        return (new DateTimeOffset(ticks, TimeSpan.Zero), count);
    }

    private enum BurstDecision
    {
        SendRequest,
        SendSummary,
        Suppress,
    }
}
