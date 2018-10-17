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
        private readonly IEventService _eventService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailService _mailService;

        public CollectionService(
            IEventService eventService,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICollectionRepository collectionRepository,
            IUserRepository userRepository,
            IMailService mailService)
        {
            _eventService = eventService;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionRepository = collectionRepository;
            _userRepository = userRepository;
            _mailService = mailService;
        }

        public async Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null,
            Guid? assignUserId = null)
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

                // Assign a user to the newly created collection.
                if(assignUserId.HasValue)
                {
                    var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, assignUserId.Value);
                    if(orgUser != null && orgUser.Status == Enums.OrganizationUserStatusType.Confirmed)
                    {
                        await _collectionRepository.UpdateUsersAsync(collection.Id,
                            new List<SelectionReadOnly> {
                                new SelectionReadOnly { Id = orgUser.Id, ReadOnly = false } });
                    }
                }

                await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Created);
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

                await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Updated);
            }
        }

        public async Task DeleteAsync(Collection collection)
        {
            await _collectionRepository.DeleteAsync(collection);
            await _eventService.LogCollectionEventAsync(collection, Enums.EventType.Collection_Deleted);
        }

        public async Task DeleteUserAsync(Collection collection, Guid organizationUserId)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
            if(orgUser == null || orgUser.OrganizationId != collection.OrganizationId)
            {
                throw new NotFoundException();
            }
            await _collectionRepository.DeleteUserAsync(collection.Id, organizationUserId);
            await _eventService.LogOrganizationUserEventAsync(orgUser, Enums.EventType.OrganizationUser_Updated);
        }
    }
}
