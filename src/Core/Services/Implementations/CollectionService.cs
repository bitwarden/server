using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICollectionUserRepository _collectionUserRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public CollectionService(
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICollectionRepository collectionRepository,
            ICollectionUserRepository collectionUserRepository,
            IUserRepository userRepository,
            IMailService mailService)
        {
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionRepository = collectionRepository;
            _collectionUserRepository = collectionUserRepository;
            _userRepository = userRepository;
            _mailService = mailService;
        }

        public async Task SaveAsync(Collection collection)
        {
            if(collection.Id == default(Guid))
            {
                var org = await _organizationRepository.GetByIdAsync(collection.OrganizationId);
                if(org == null)
                {
                    throw new BadRequestException("Org not found");
                }

                if(org.MaxCollections.HasValue)
                {
                    var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(org.Id);
                    if(org.MaxCollections.Value <= collectionCount)
                    {
                        throw new BadRequestException("You have reached the maximum number of collections " +
                        $"({org.MaxCollections.Value}) for this organization.");
                    }
                }

                await _collectionRepository.CreateAsync(collection);
            }
            else
            {
                await _collectionRepository.ReplaceAsync(collection);
            }
        }
    }
}
