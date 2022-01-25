using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    public abstract class SendSponsorshipOfferCommand
    {
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;
        private readonly IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> _tokenFactory;

        public SendSponsorshipOfferCommand(IUserRepository userRepository,
            IMailService mailService,
            IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable> tokenFactory)
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
                isExistingAccount, _tokenFactory.Protect(new OrganizationSponsorshipOfferTokenable(sponsorship)));
        }
    }
}
