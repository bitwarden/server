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
        _accessMailNotifier = accessMailNotifier;
        _organizationRepository = organizationRepository;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _logger = logger;
    }

    public async Task NotifyLeaseEndedAsync(AccessLease lease, AccessLeaseAction endAction)
    {
        if (endAction != AccessLeaseAction.Revoked)
        {
            return;
        }

        // Duplicates the guard inside IAccessMailNotifier to keep the organization read off every revocation in the
        // flag-off state — which is every revocation on self-host, and every one in cloud until the flag is on.
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamEmailNotifications))
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
            // Ids only: not the holder's address, and never the reason the operator gave for revoking.
            _logger.LogError(ex, "PAM lease-revoked mail for lease {AccessLeaseId} could not be sent.", lease.Id);
        }
    }
}
