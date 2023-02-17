namespace Bit.Core.Models.Data.Organizations.Policies;

public class MasterPasswordPolicyData : IPolicyDataModel
{
    public int MinComplexity { get; set; }

    public int MinLength { get; set; }

    public bool RequireUpper { get; set; }

    public bool RequireNumbers { get; set; }

    public bool RequireSpecial { get; set; }

    public bool EnforceOnLogin { get; set; }
}
