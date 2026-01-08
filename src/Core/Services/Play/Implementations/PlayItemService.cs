using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class PlayItemService(IPlayIdService playIdService, IPlayItemRepository playItemRepository, ILogger<PlayItemService> logger) : IPlayItemService
{
    public async Task Record(User user)
    {
        if (playIdService.InPlay(out var playId))
        {
            logger.LogInformation("Associating user {UserId} with Play ID {PlayId}", user.Id, playId);
            await playItemRepository.CreateAsync(PlayItem.Create(user, playId));
        }
    }
    public async Task Record(Organization organization)
    {
        if (playIdService.InPlay(out var playId))
        {
            logger.LogInformation("Associating organization {OrganizationId} with Play ID {PlayId}", organization.Id, playId);
            await playItemRepository.CreateAsync(PlayItem.Create(organization, playId));
        }
    }
}
