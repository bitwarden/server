using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api.Response.Accounts;

public class PreloginResponseModel
{
    public PreloginResponseModel(UserKdfInformation kdfInformation)
    {
        Kdf = kdfInformation.Kdf;
        KdfIterations = kdfInformation.KdfIterations;
        KdfMemory = kdfInformation.KdfMemory;
        KdfParallelism = kdfInformation.KdfParallelism;
    }

    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
}
