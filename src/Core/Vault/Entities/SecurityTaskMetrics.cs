namespace Bit.Core.Vault.Entities;

public class SecurityTaskMetrics
{
    public SecurityTaskMetrics(int completedTasksCount, int totalTasksCount)
    {
        CompletedTasksCount = completedTasksCount;
        TotalTasksCount = totalTasksCount;
    }

    public int CompletedTasksCount { get; set; }
    public int TotalTasksCount { get; set; }
}
