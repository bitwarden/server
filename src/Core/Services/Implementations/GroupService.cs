using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class GroupService : IGroupService
{
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IReferenceEventService _referenceEventService;

    public GroupService(
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        IReferenceEventService referenceEventService)
    {
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _groupRepository = groupRepository;
        _referenceEventService = referenceEventService;
    }

    public async Task SaveAsync(Group group, IEnumerable<CollectionAccessSelection> collections = null)
    {
        var org = await _organizationRepository.GetByIdAsync(group.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UseGroups)
        {
            throw new BadRequestException("This organization cannot use groups.");
        }

        if (group.Id == default(Guid))
        {
            group.CreationDate = group.RevisionDate = DateTime.UtcNow;

            if (collections == null)
            {
                await _groupRepository.CreateAsync(group);
            }
            else
            {
                await _groupRepository.CreateAsync(group, collections);
            }

            await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Created);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.GroupCreated, org));
        }
        else
        {
            group.RevisionDate = DateTime.UtcNow;

            if (collections == null)
            {
                await _groupRepository.ReplaceAsync(group);
            }
            else
            {
                await _groupRepository.ReplaceAsync(group, collections);
            }

            await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Updated);
        }
    }

    public async Task DeleteAsync(Group group)
    {
        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, Enums.EventType.Group_Deleted);
    }

    public async Task DeleteUserAsync(Group group, Guid organizationUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != group.OrganizationId)
        {
            throw new NotFoundException();
        }
        await _groupRepository.DeleteUserAsync(group.Id, organizationUserId);
        await _eventService.LogOrganizationUserEventAsync(orgUser, Enums.EventType.OrganizationUser_UpdatedGroups);
    }
}
