using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public CollectionService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICollectionRepository collectionRepository,
            IUserRepository userRepository,
            IMailService mailService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionRepository = collectionRepository;
            _userRepository = userRepository;
            _mailService = mailService;
        }

        public async Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null)
        {
            var org = await _organizationRepository.GetByIdAsync(collection.OrganizationId);
            if(org == null)
            {
                throw new BadRequestException("Organization not found");
            }

            if(collection.Id == default(Guid))
            {
                if(org.MaxCollections.HasValue)
                {
                    var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(org.Id);
                    if(org.MaxCollections.Value <= collectionCount)
                    {
                        throw new BadRequestException("You have reached the maximum number of collections " +
                        $"({org.MaxCollections.Value}) for this organization.");
                    }
                }

                if(groups == null || !org.UseGroups)
                {
                    await _collectionRepository.CreateAsync(collection);
                }
                else
                {
                    await _collectionRepository.CreateAsync(collection, groups);
                }
            }
            else
            {
                if(!org.UseGroups)
                {
                    await _collectionRepository.ReplaceAsync(collection);
                }
                else
                {
                    await _collectionRepository.ReplaceAsync(collection, groups ?? new List<SelectionReadOnly>());
                }
            }
        }
    }
}
