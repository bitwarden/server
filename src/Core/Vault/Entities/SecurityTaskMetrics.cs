namespace Bit.Core.Vault.Entities;

public class SecurityTaskMetrics
{
    public SecurityTaskMetrics(int completedTasks, int totalTasks)
    {
        CompletedTasks = completedTasks;
        TotalTasks = totalTasks;
    }

    public int CompletedTasks { get; set; }
    public int TotalTasks { get; set; }
}
