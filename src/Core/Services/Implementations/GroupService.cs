using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using System.Collections.Generic;

namespace Bit.Core.Services
{
    public class GroupService : IGroupService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IGroupRepository _groupRepository;

        public GroupService(
            IOrganizationRepository organizationRepository,
            IGroupRepository groupRepository)
        {
            _organizationRepository = organizationRepository;
            _groupRepository = groupRepository;
        }

        public async Task SaveAsync(Group group, IEnumerable<Guid> collectionIds = null)
        {
            var org = await _organizationRepository.GetByIdAsync(group.OrganizationId);
            if(org == null)
            {
                throw new BadRequestException("Organization not found");
            }

            if(!org.UseGroups)
            {
                throw new BadRequestException("This organization cannot use groups.");
            }

            if(group.Id == default(Guid))
            {
                if(collectionIds == null)
                {
                    await _groupRepository.CreateAsync(group);
                }
                else
                {
                    await _groupRepository.CreateAsync(group, collectionIds);
                }
            }
            else
            {
                if(collectionIds == null)
                {
                    await _groupRepository.ReplaceAsync(group);
                }
                else
                {
                    await _groupRepository.ReplaceAsync(group, collectionIds);
                }
            }
        }
    }
}
