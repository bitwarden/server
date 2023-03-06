using Bit.Core.Models.Data.Organizations.Policies;

namespace Bit.Core.Models.Api.Response;

public class MasterPasswordPolicyResponseModel : ResponseModel
{
    public MasterPasswordPolicyResponseModel(MasterPasswordPolicyData data) : base("masterPasswordPolicy")
    {
        if (data == null)
        {
            return;
        }

        MinComplexity = data.MinComplexity ?? 0;
        MinLength = data.MinLength ?? 0;
        RequireUpper = data.RequireUpper ?? false;
        RequireNumbers = data.RequireNumbers ?? false;
        RequireSpecial = data.RequireSpecial ?? false;
        EnforceOnLogin = data.EnforceOnLogin ?? false;
    }

    public int MinComplexity { get; set; }

    public int MinLength { get; set; }

    public bool RequireUpper { get; set; }

    public bool RequireNumbers { get; set; }

    public bool RequireSpecial { get; set; }

    public bool EnforceOnLogin { get; set; }
}
