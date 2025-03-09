#nullable enable
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;

namespace Bit.Api.KeyManagement.Models.Requests;

public class AccountDataRequestModel
{
    public required IEnumerable<CipherWithIdRequestModel> Ciphers { get; set; }
    public required IEnumerable<FolderWithIdRequestModel> Folders { get; set; }
    public required IEnumerable<SendWithIdRequestModel> Sends { get; set; }
}
