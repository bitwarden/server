using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class PolicyService : IPolicyService
    {
        private readonly IEventService _eventService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPolicyRepository _policyRepository;

        public PolicyService(
            IEventService eventService,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPolicyRepository policyRepository)
        {
            _eventService = eventService;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _policyRepository = policyRepository;
        }

        public async Task SaveAsync(Policy policy)
        {
            var org = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
            if(org == null)
            {
                throw new BadRequestException("Organization not found");
            }

            /*
            if(!org.UseGroups)
            {
                throw new BadRequestException("This organization cannot use policies.");
            }
            */

            if(policy.Id == default(Guid))
            {
                policy.CreationDate = policy.RevisionDate = DateTime.UtcNow;
                await _policyRepository.CreateAsync(policy);
                await _eventService.LogPolicyEventAsync(policy, Enums.EventType.Policy_Created);
            }
            else
            {
                policy.RevisionDate = DateTime.UtcNow;
                await _policyRepository.ReplaceAsync(policy);
                await _eventService.LogPolicyEventAsync(policy, Enums.EventType.Policy_Updated);
            }
        }

        public async Task DeleteAsync(Policy policy)
        {
            await _policyRepository.DeleteAsync(policy);
            await _eventService.LogPolicyEventAsync(policy, Enums.EventType.Policy_Deleted);
        }
    }
}
