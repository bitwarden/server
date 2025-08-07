namespace Bit.Api.Vault.Models.Response;

public class SecurityTaskMetricsResponseModel
{

    public SecurityTaskMetricsResponseModel(int completedTasksCount, int totalTasksCount)
    {
        this.completedTasksCount = completedTasksCount;
        this.totalTasksCount = totalTasksCount;
    }

    /// <summary>
    /// Number of tasks that have been completed in the organization.
    /// </summary>
    public int completedTasksCount { get; set; }

    /// <summary>
    /// Total number of tasks in the organization, regardless of their status.
    /// </summary>
    public int totalTasksCount { get; set; }
}
