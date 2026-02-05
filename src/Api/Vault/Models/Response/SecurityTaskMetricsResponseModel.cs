namespace Bit.Api.Vault.Models.Response;

public class SecurityTaskMetricsResponseModel
{

    public SecurityTaskMetricsResponseModel(int completedTasks, int totalTasks)
    {
        CompletedTasks = completedTasks;
        TotalTasks = totalTasks;
    }

    /// <summary>
    /// Number of tasks that have been completed in the organization.
    /// </summary>
    public int CompletedTasks { get; set; }

    /// <summary>
    /// Total number of tasks in the organization, regardless of their status.
    /// </summary>
    public int TotalTasks { get; set; }
}
