using Bit.Core.Models.Api;

namespace Bit.Api.SecretsManager.Models.Response;

public class ProjectCountsResponseModel() : ResponseModel(_objectName)
{
    private const string _objectName = "project_counts";


    public int Secrets { get; set; }

    public int People { get; set; }

    public int ServiceAccounts { get; set; }
}
