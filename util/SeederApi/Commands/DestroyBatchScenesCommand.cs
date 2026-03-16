using Bit.SeederApi.Commands.Interfaces;

namespace Bit.SeederApi.Commands;

public class DestroyBatchScenesCommand(
    ILogger<DestroyBatchScenesCommand> logger,
    IDestroySceneCommand destroySceneCommand) : IDestroyBatchScenesCommand
{
    public async Task DestroyAsync(IEnumerable<string> playIds)
    {
        var exceptions = new List<Exception>();

        foreach (var playId in playIds)
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
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more errors occurred while deleting seeded data", exceptions);
        }
    }
}
