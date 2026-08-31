using Bit.Core;
using Bit.Core.Pam.Models.Mail.AccessRequestDecided;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

public class RequesterMailNotifier : IRequesterMailNotifier
{
    private readonly IAccessMailNotifier _accessMailNotifier;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly ILogger<RequesterMailNotifier> _logger;

    public RequesterMailNotifier(
        IAccessMailNotifier accessMailNotifier,
        IOrganizationRepository organizationRepository,
        IGlobalSettings globalSettings,
        IFeatureService featureService,
        ILogger<RequesterMailNotifier> logger)
    {
        _accessMailNotifier = accessMailNotifier;
        _organizationRepository = organizationRepository;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _logger = logger;
    }

    public async Task NotifyDecisionAsync(AccessRequest request, bool approved)
    {
        // Duplicates the guard inside IAccessMailNotifier to keep the organization read off every decision in the
        // flag-off state — which is every decision on self-host.
        if (!_featureService.IsEnabled(FeatureFlagKeys.Pam))
        {
            return;
        }

        try
        {
            var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId);
            if (organization is null)
            {
                _logger.LogWarning(
                    "PAM decision mail for access request {AccessRequestId}: organization could not be resolved; nothing sent.",
                    request.Id);
                return;
            }

            var view = new AccessRequestDecidedView
            {
                WebVaultUrl = _globalSettings.BaseServiceUri.VaultWithHash,
                AccessRequestId = request.Id,
                OrganizationName = organization.Name,
                Approved = approved,
                NotBefore = request.NotBefore,
                NotAfter = request.NotAfter,
            };

            await _accessMailNotifier.SendToUserAsync(
                request.RequesterId, email => new AccessRequestDecidedMail(email, view));
        }
        catch (Exception ex)
        {
            // Ids only: not the requester's address, and never the request's reason or the approver's comment.
            _logger.LogError(ex,
                "PAM decision mail for access request {AccessRequestId} could not be sent.", request.Id);
        }
    }
}
