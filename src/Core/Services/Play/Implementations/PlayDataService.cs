using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class PlayDataService(IPlayIdService playIdService, IPlayDataRepository playDataRepository, ILogger<PlayDataService> logger) : IPlayDataService
{
    public async Task Record(User user)
    {
        if (playIdService.InPlay(out var playId))
        {
            logger.LogInformation("Associating user {UserId} with Play ID {PlayId}", user.Id, playId);
            await playDataRepository.CreateAsync(PlayData.Create(user, playId));
        }
    }
    public async Task Record(Organization organization)
    {
        if (playIdService.InPlay(out var playId))
        {
            logger.LogInformation("Associating organization {OrganizationId} with Play ID {PlayId}", organization.Id, playId);
            await playDataRepository.CreateAsync(PlayData.Create(organization, playId));
        }
    }
}
