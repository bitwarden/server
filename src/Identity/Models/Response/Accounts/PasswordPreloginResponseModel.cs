using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Models.Data;

namespace Bit.Identity.Models.Response.Accounts;

public class PasswordPreloginResponseModel
{
    public PasswordPreloginResponseModel(UserKdfInformation kdfInformation, string? salt = null)
    {
        // PM-28143 Cleanup
        Kdf = kdfInformation.Kdf;
        KdfIterations = kdfInformation.KdfIterations;
        KdfMemory = kdfInformation.KdfMemory;
        KdfParallelism = kdfInformation.KdfParallelism;
        // End Cleanup

        KdfSettings = new KdfSettings()
        {
            KdfType = kdfInformation.Kdf,
            Iterations = kdfInformation.KdfIterations,
            Memory = kdfInformation.KdfMemory,
            Parallelism = kdfInformation.KdfParallelism,
        };
        Salt = salt;
    }

    // Old Data Types
    public KdfType? Kdf { get; set; }           // PM-28143 Remove with cleanup
    public int? KdfIterations { get; set; }     // PM-28143 Remove with cleanup
    public int? KdfMemory { get; set; }         // PM-28143 Remove with cleanup
    public int? KdfParallelism { get; set; }    // PM-28143 Remove with cleanup

    // New Data Types
    public KdfSettings? KdfSettings { get; set; }  // PM-28143 With cleanup make this not nullish
    public string? Salt { get; set; }              // PM-28143 With cleanup make this not nullish. Not used yet,
                                                   // just the email from the request at this time.
}
