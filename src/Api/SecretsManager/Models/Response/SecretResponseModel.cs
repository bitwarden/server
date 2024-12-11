using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class SecretResponseModel : BaseSecretResponseModel
{
    private const string _objectName = "secret";

    public SecretResponseModel(Secret secret, bool read, bool write)
        : base(secret, _objectName)
    {
        Read = read;
        Write = write;
    }

    public SecretResponseModel()
        : base(_objectName) { }

    public bool Read { get; set; }

    public bool Write { get; set; }
}
