using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

public class ModifyCollectionUserAccessCommand(
    ICollectionRepository collectionRepository,
    IModifyCollectionUserAccessValidator validator,
    IEventService eventService,
    TimeProvider timeProvider) : IModifyCollectionUserAccessCommand
{
    public async Task<CommandResult> ModifyAsync(ModifyCollectionUserAccessRequest request)
    {
        // Nothing to do, so skip saving and logging.
        if (request.Add.Count == 0 && request.Update.Count == 0 && request.Remove.Count == 0)
        {
            return new None();
        }

        var validationResult = await validator.ValidateAsync(request);
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var revisionDate = timeProvider.GetUtcNow().UtcDateTime;
        var upserts = request.Add.Concat(request.Update).ToList();

        // Drop ids that aren't members, so we don't bump an unrelated user's revision date.
        var existingUserIds = request.Targets
            .SelectMany(t => t.AccessDetails.Users.Select(u => u.Id))
            .ToHashSet();
        var removeIds = request.Remove.Where(existingUserIds.Contains).ToList();

        var organizationId = request.Targets.First().Collection.OrganizationId;
        var collectionIds = request.Targets.Select(t => t.Collection.Id).ToList();

        await collectionRepository.ModifyUserAccessAsync(organizationId, collectionIds, upserts, removeIds, revisionDate);

        await eventService.LogCollectionEventsAsync(
            request.Targets.Select(t => (t.Collection, EventType.Collection_Updated, (DateTime?)revisionDate)));

        return new None();
    }
}
