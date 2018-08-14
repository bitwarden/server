using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class PreloginResponseModel
    {
        public PreloginResponseModel(UserKdfInformation kdfInformation)
        {
            Kdf = kdfInformation.Kdf;
            KdfIterations = kdfInformation.KdfIterations;
        }

        public KdfType Kdf { get; set; }
        public int KdfIterations { get; set; }
    }
}
