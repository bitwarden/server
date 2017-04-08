using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class SubvaultService : ISubvaultService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ISubvaultRepository _subvaultRepository;
        private readonly ISubvaultUserRepository _subvaultUserRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public SubvaultService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ISubvaultRepository subvaultRepository,
            ISubvaultUserRepository subvaultUserRepository,
            IUserRepository userRepository,
            IMailService mailService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _subvaultRepository = subvaultRepository;
            _subvaultUserRepository = subvaultUserRepository;
            _userRepository = userRepository;
            _mailService = mailService;
        }

        public async Task SaveAsync(Subvault subvault)
        {
            if(subvault.Id == default(Guid))
            {
                var org = await _organizationRepository.GetByIdAsync(subvault.OrganizationId);
                if(org == null)
                {
                    throw new BadRequestException("Org not found");
                }

                if(org.MaxSubvaults.HasValue)
                {
                    var subvaultCount = await _subvaultRepository.GetCountByOrganizationIdAsync(org.Id);
                    if(org.MaxSubvaults.Value <= subvaultCount)
                    {
                        throw new BadRequestException("You have reached the maximum number of subvaults " +
                        $"({org.MaxSubvaults.Value}) for this organization.");
                    }
                }

                await _subvaultRepository.CreateAsync(subvault);
            }
            else
            {
                await _subvaultRepository.ReplaceAsync(subvault);
            }
        }
    }
}
