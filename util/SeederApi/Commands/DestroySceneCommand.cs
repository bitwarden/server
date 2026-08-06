using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.SeederApi.Commands.Interfaces;
using Bit.SeederApi.Services;

namespace Bit.SeederApi.Commands;

public class DestroySceneCommand(
    DatabaseContext databaseContext,
    ILogger<DestroySceneCommand> logger,
    IUserRepository userRepository,
    IPlayItemRepository playItemRepository,
    IProviderRepository providerRepository,
    IOrganizationRepository organizationRepository) : IDestroySceneCommand
{
    public async Task<object?> DestroyAsync(string playId)
    {
        // Note, delete cascade will remove PlayItem entries

        var playItem = await playItemRepository.GetByPlayIdAsync(playId);
        var userIds = playItem.Select(pd => pd.UserId).Distinct().ToList();
        var organizationIds = playItem.Select(pd => pd.OrganizationId).Distinct().ToList();
        var providerIds = playItem
            .Select(pd => pd.ProviderId)
            .Where(id => id.HasValue)
            .Distinct()
            .ToList();

        // Delete Providers first. ProviderUser/ProviderOrganization/ProviderPlan cascade from the Provider,
        // but FK_ProviderUser_User and FK_ProviderOrganization_Organization do not — so the provider link
        // rows must be gone before the users and organizations below can be deleted.
        if (providerIds.Count > 0)
        {
            var providers = databaseContext.Providers.Where(p => providerIds.Contains(p.Id)).ToList();
            var providerAggregateException = new AggregateException();
            foreach (var provider in providers)
            {
                try
                {
                    await providerRepository.DeleteAsync(provider);
                }
                catch (Exception ex)
                {
                    providerAggregateException = new AggregateException(providerAggregateException, ex);
                }
            }
            if (providerAggregateException.InnerExceptions.Count > 0)
            {
                throw new SceneExecutionException(
                    $"One or more errors occurred while deleting providers for seed ID {playId}",
                    providerAggregateException);
            }
        }

        // Delete Users before Organizations to respect foreign key constraints
        if (userIds.Count > 0)
        {
            var users = databaseContext.Users.Where(u => userIds.Contains(u.Id));
            await userRepository.DeleteManyAsync(users);
        }

        if (organizationIds.Count > 0)
        {
            var organizations = databaseContext.Organizations.Where(o => organizationIds.Contains(o.Id)).ToList();
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
