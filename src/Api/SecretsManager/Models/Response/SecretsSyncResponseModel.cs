#nullable enable
using Bit.Api.Models.Response;
using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretsSyncResponseModel : ResponseModel
{
    private const string _objectName = "secretsSync";

    public bool HasChanges { get; set; }
    public ListResponseModel<BaseSecretResponseModel>? Secrets { get; set; }

    public SecretsSyncResponseModel(bool hasChanges, IEnumerable<Secret>? secrets, string obj = _objectName)
        : base(obj)
    {
        Secrets = secrets != null
            ? new ListResponseModel<BaseSecretResponseModel>(secrets.Select(s => new BaseSecretResponseModel(s)))
            : null;
        HasChanges = hasChanges;
    }

    public SecretsSyncResponseModel() : base(_objectName)
    {
    }
}
