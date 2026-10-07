using Bit.Core;
using Bit.Core.Pam.Models.Mail.AccessLeaseRevoked;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

public class LeaseRevokedMailNotifier : ILeaseRevokedMailNotifier
{
    private readonly IAccessMailNotifier _accessMailNotifier;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly ILogger<LeaseRevokedMailNotifier> _logger;

    public LeaseRevokedMailNotifier(
        IAccessMailNotifier accessMailNotifier,
        IOrganizationRepository organizationRepository,
        IGlobalSettings globalSettings,
        IFeatureService featureService,
        ILogger<LeaseRevokedMailNotifier> logger)
    {
        _accessMailNotifier = accessMailNotifier ?? throw new ArgumentNullException(nameof(accessMailNotifier));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyLeaseEndedAsync(AccessLease lease, AccessLeaseAction endAction)
    {
        if (endAction != AccessLeaseAction.Revoked)
        {
            return;
        }

        // Repeats IAccessMailNotifier's flag guard to skip the organization read when the flag is off, as on self-host.
        if (!_featureService.IsEnabled(FeatureFlagKeys.Pam))
        {
            return;
        }

        try
        {
            var organization = await _organizationRepository.GetByIdAsync(lease.OrganizationId);
            if (organization is null)
            {
                _logger.LogWarning(
                    "PAM lease-revoked mail for lease {AccessLeaseId}: organization could not be resolved; nothing sent.",
                    lease.Id);
                return;
            }

            var view = new AccessLeaseRevokedView
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                AccessRequestId = lease.AccessRequestId,
                OrganizationName = organization.Name,
                NotAfter = lease.NotAfter,
            };

            await _accessMailNotifier.SendToUserAsync(
                lease.RequesterId, email => new AccessLeaseRevokedMail { ToEmails = [email], View = view });
        }
        catch (Exception ex)
        {
            // Ids only; no address and never the operator's revoke reason.
            _logger.LogError(ex, "PAM lease-revoked mail for lease {AccessLeaseId} could not be sent.", lease.Id);
        }
    }
}
