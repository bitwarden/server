namespace Bit.Core.Vault.Commands.Interfaces;

public interface IMarkNotificationsForTaskAsDeletedCommand
{
    /// <summary>
    /// Marks notifications associated with a given taskId as deleted.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to complete</param>
    /// <returns>A task representing the async operation</returns>
    Task MarkAsDeletedAsync(Guid taskId);
}
