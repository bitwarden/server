namespace Bit.Core.Vault.Commands.Interfaces;

public interface IMarkTaskAsCompleteCommand
{
    /// <summary>
    /// Marks a task as complete.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to complete</param>
    /// <returns>A task representing the async operation</returns>
    Task CompleteAsync(Guid taskId);
}
