namespace Bit.Core.Vault.Entities;

public class SecurityTaskMetrics
{
    public SecurityTaskMetrics(int completedTasksCount, int totalTasksCount)
    {
        this.completedTasksCount = completedTasksCount;
        this.totalTasksCount = totalTasksCount;
    }

    public int completedTasksCount { get; set; }
    public int totalTasksCount { get; set; }
}
