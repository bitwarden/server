using Bit.SeederApi.Commands.Interfaces;

namespace Bit.SeederApi.Commands;

public class DestroyBatchScenesCommand(
    ILogger<DestroyBatchScenesCommand> logger,
    IDestroySceneCommand destroySceneCommand) : IDestroyBatchScenesCommand
{
    public async Task DestroyAsync(IEnumerable<string> playIds)
    {
        var exceptions = new List<Exception>();

        var deleteTasks = playIds.Select(async playId =>
        {
            try
            {
                await destroySceneCommand.DestroyAsync(playId);
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
                logger.LogError(ex, "Error deleting seeded data: {PlayId}", playId);
            }
        });

        await Task.WhenAll(deleteTasks);

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more errors occurred while deleting seeded data", exceptions);
        }
    }
}
