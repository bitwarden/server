using Bit.Core.Auth.Models.Api.Request.Opaque;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Identity.Models.Response.Accounts;

public class PreloginResponseModel
{
    public PreloginResponseModel(UserKdfInformation kdfInformation, OpaqueKeyExchangeCipherConfiguration opaqueConfiguration)
    {
        Kdf = kdfInformation.Kdf;
        KdfIterations = kdfInformation.KdfIterations;
        KdfMemory = kdfInformation.KdfMemory;
        KdfParallelism = kdfInformation.KdfParallelism;
        OpaqueConfiguration = opaqueConfiguration;
    }

    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
    public OpaqueKeyExchangeCipherConfiguration OpaqueConfiguration { get; set; }
}
