using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.Models.Api.Response;

public class MasterPasswordPolicyResponseModel : ResponseModel
{
    public MasterPasswordPolicyResponseModel(MasterPasswordPolicyData data)
        : base("masterPasswordPolicy")
    {
        if (data == null)
        {
            return;
        }

        MinComplexity = data.MinComplexity;
        MinLength = data.MinLength;
        RequireLower = data.RequireLower;
        RequireUpper = data.RequireUpper;
        RequireNumbers = data.RequireNumbers;
        RequireSpecial = data.RequireSpecial;
        EnforceOnLogin = data.EnforceOnLogin;
    }

    public int? MinComplexity { get; set; }

    public int? MinLength { get; set; }

    public bool? RequireLower { get; set; }

    public bool? RequireUpper { get; set; }

    public bool? RequireNumbers { get; set; }

    public bool? RequireSpecial { get; set; }

    public bool? EnforceOnLogin { get; set; }
}
