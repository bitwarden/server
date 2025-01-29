using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;

namespace Bit.Api.KeyManagement.Models.Request;

public class AccountDataRequestModel
{
    public IEnumerable<CipherWithIdRequestModel> Ciphers { get; set; }
    public IEnumerable<FolderWithIdRequestModel> Folders { get; set; }
    public IEnumerable<SendWithIdRequestModel> Sends { get; set; }
}
