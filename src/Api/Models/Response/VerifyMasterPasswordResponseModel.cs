using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Response;

namespace Bit.Api.Models.Response;

public class VerifyMasterPasswordResponseModel : ResponseModel
{
    public VerifyMasterPasswordResponseModel(IEnumerable<PolicyResponseModel> masterPasswordPolicies = null) : base("secretVerification")
    {
        MasterPasswordPolicies = masterPasswordPolicies;
    }

    public IEnumerable<PolicyResponseModel> MasterPasswordPolicies { get; set; }
}
