using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Response;

namespace Bit.Api.Models.Response;

public class VerifyMasterPasswordResponseModel : ResponseModel
{
    public VerifyMasterPasswordResponseModel(MasterPasswordPolicyResponseModel masterPasswordPolicy) : base("verifyMasterPassword")
    {
        MasterPasswordPolicy = masterPasswordPolicy;
    }
    public MasterPasswordPolicyResponseModel MasterPasswordPolicy { get; set; }
}
