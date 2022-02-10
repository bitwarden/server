using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables.Cloud;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    public class CloudSendSponsorshipOfferCommand : ISendSponsorshipOfferCommand
    {
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;
        private readonly IDataProtectorTokenFactory<CloudOrganizationSponsorshipOfferTokenable> _tokenFactory;

        public CloudSendSponsorshipOfferCommand(IUserRepository userRepository,
            IMailService mailService,
            IDataProtectorTokenFactory<CloudOrganizationSponsorshipOfferTokenable> tokenFactory)
        {
            _userRepository = userRepository;
            _mailService = mailService;
            _tokenFactory = tokenFactory;
        }

        public async Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship, string sponsoringEmail)
        {
            var user = await _userRepository.GetByEmailAsync(sponsorship.OfferedToEmail);
            var isExistingAccount = user != null;

            await _mailService.SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, sponsoringEmail,
                isExistingAccount, _tokenFactory.Protect(new CloudOrganizationSponsorshipOfferTokenable(sponsorship)));
        }

        public async Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, string sponsoringUserEmail)
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

            await SendSponsorshipOfferAsync(sponsorship, sponsoringUserEmail);
        }
    }
}
