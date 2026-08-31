using Bit.Core;
using Bit.Core.Pam.Models.Mail.AccessRequestPending;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

public class ApproverMailNotifier : IApproverMailNotifier
{
    private readonly IAccessMailNotifier _accessMailNotifier;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly ILogger<ApproverMailNotifier> _logger;

    public ApproverMailNotifier(
        IAccessMailNotifier accessMailNotifier,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IGlobalSettings globalSettings,
        IFeatureService featureService,
        ILogger<ApproverMailNotifier> logger)
    {
        _accessMailNotifier = accessMailNotifier;
        _collectionRepository = collectionRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _logger = logger;
    }

    public async Task NotifyPendingRequestAsync(AccessRequest request)
    {
        // Duplicates the guard inside IAccessMailNotifier so the organization and requester reads below do not run on
        // every submission in the flag-off state -- which is every submission on self-host.
        if (!_featureService.IsEnabled(FeatureFlagKeys.Pam))
        {
            return;
        }

        try
        {
            // A requester may well manage the collection they are requesting against -- an org Owner always does when
            // AllowAdminAccessToAllCollectionItems is on -- but DecideAccessRequestCommand refuses a self-decision, so
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

            var view = new AccessRequestPendingView
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                AccessRequestId = request.Id,
                OrganizationName = organization.Name,
                RequesterEmail = requester.Email,
                NotBefore = request.NotBefore,
                NotAfter = request.NotAfter,
            };

            await _accessMailNotifier.SendToUsersAsync(approverIds,
                email => new AccessRequestPendingMail { ToEmails = [email], View = view });
        }
        catch (Exception ex)
        {
            // Ids only: neither the approvers' nor the requester's address, and never the request's reason.
            _logger.LogError(ex,
                "PAM pending-request mail for access request {AccessRequestId} could not be sent.", request.Id);
        }
    }
}
