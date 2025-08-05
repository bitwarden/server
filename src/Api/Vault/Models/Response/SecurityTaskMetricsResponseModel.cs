namespace Bit.Api.Vault.Models.Response;

public class SecurityTasksMetricsResponseModel
{
    public SecurityTasksMetricsResponseModel(int completedTasksCount, int totalTasksCount)
    {
        this.completedTasksCount = completedTasksCount;
        this.totalTasksCount = totalTasksCount;
    }

    public int completedTasksCount { get; set; }
    public int totalTasksCount { get; set; }
}
