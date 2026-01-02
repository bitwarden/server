using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.SeederApi.Commands.Interfaces;
using Bit.SeederApi.Services;

namespace Bit.SeederApi.Commands;

public class DestroySceneCommand(
    DatabaseContext databaseContext,
    ILogger<DestroySceneCommand> logger,
    IUserRepository userRepository,
    IPlayDataRepository playDataRepository,
    IOrganizationRepository organizationRepository) : IDestroySceneCommand
{
    public async Task<object?> DestroyAsync(string playId)
    {
        // Note, delete cascade will remove PlayData entries

        var playData = await playDataRepository.GetByPlayIdAsync(playId);
        var userIds = playData.Select(pd => pd.UserId).Distinct().ToList();
        var organizationIds = playData.Select(pd => pd.OrganizationId).Distinct().ToList();

        // Delete Users before Organizations to respect foreign key constraints
        if (userIds.Count > 0)
        {
            var users = databaseContext.Users.Where(u => userIds.Contains(u.Id));
            await userRepository.DeleteManyAsync(users);
        }

        if (organizationIds.Count > 0)
        {
            var organizations = databaseContext.Organizations.Where(o => organizationIds.Contains(o.Id));
            var aggregateException = new AggregateException();
            foreach (var org in organizations)
            {
                try
                {
                    await organizationRepository.DeleteAsync(org);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                }
            }
            if (aggregateException.InnerExceptions.Count > 0)
            {
                throw new SceneExecutionException(
                    $"One or more errors occurred while deleting organizations for seed ID {playId}",
                    aggregateException);
            }
        }

        logger.LogInformation("Successfully destroyed seeded data with ID {PlayId}", playId);

        return new { PlayId = playId };
    }
}
