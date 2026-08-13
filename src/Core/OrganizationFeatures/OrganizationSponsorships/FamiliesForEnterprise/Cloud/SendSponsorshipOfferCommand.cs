// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

public class SendSponsorshipOfferCommand : ISendSponsorshipOfferCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IMailService _mailService;
    private readonly IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> _tokenFactory;
    private readonly IFeatureService _featureService;

    public SendSponsorshipOfferCommand(IUserRepository userRepository,
        IMailService mailService,
        IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> tokenFactory,
        IFeatureService featureService)
    {
        _userRepository = userRepository;
        _mailService = mailService;
        _tokenFactory = tokenFactory;
        _featureService = featureService;
    }

    public async Task BulkSendSponsorshipOfferAsync(string sponsoringOrgName, IEnumerable<OrganizationSponsorship> sponsorships)
    {
        var vfo1FoundationEnabled = _featureService.IsEnabled(FeatureFlagKeys.VFO1Foundation);

        var invites = new List<(string, bool, string)>();
        foreach (var sponsorship in sponsorships)
        {
            var user = await _userRepository.GetByEmailAsync(sponsorship.OfferedToEmail);
            var isExistingAccount = user != null;
            invites.Add((sponsorship.OfferedToEmail, user != null, _tokenFactory.Protect(new OrganizationSponsorshipOfferTokenable(sponsorship))));
        }

        await _mailService.BulkSendFamiliesForEnterpriseOfferEmailAsync(sponsoringOrgName, invites, vfo1FoundationEnabled);
    }

    public async Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship, string sponsoringOrgName)
    {
        var user = await _userRepository.GetByEmailAsync(sponsorship.OfferedToEmail);
        var isExistingAccount = user != null;
        var vfo1FoundationEnabled = _featureService.IsEnabled(FeatureFlagKeys.VFO1Foundation);

        await _mailService.SendFamiliesForEnterpriseOfferEmailAsync(sponsoringOrgName, sponsorship.OfferedToEmail,
            isExistingAccount, _tokenFactory.Protect(new OrganizationSponsorshipOfferTokenable(sponsorship)), vfo1FoundationEnabled);
    }

    public async Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
        OrganizationSponsorship sponsorship)
    {
        if (sponsoringOrg == null)
        {
            throw new BadRequestException("Cannot find the requested sponsoring organization.");
        }

        if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
        {
            throw new BadRequestException("Only confirmed users can sponsor other organizations.");
        }

        if (sponsorship == null || sponsorship.OfferedToEmail == null)
        {
            throw new BadRequestException("Cannot find an outstanding sponsorship offer for this organization.");
        }

        await SendSponsorshipOfferAsync(sponsorship, sponsoringOrg.DisplayName());
    }
}
