namespace Bit.Core.Models.Data.Organizations.Policies;

public class MasterPasswordPolicyData : IPolicyDataModel
{
    public int? MinComplexity { get; set; } = 0;
    public int? MinLength { get; set; } = 0;
    public bool? RequireUpper { get; set; } = false;
    public bool? RequireNumbers { get; set; } = false;
    public bool? RequireSpecial { get; set; } = false;
    public bool? EnforceOnLogin { get; set; } = false;

    /// <summary>
    /// Combine the other policy data with this instance, taking the most secure options
    /// </summary>
    /// <param name="other">The other policy instance to combine with this</param>
    public void CombineWith(MasterPasswordPolicyData other)
    {
        if (other == null)
        {
            return;
        }

        if (other.MinComplexity.HasValue && other.MinComplexity > MinComplexity)
        {
            MinComplexity = other.MinComplexity;
        }

        if (other.MinLength.HasValue && other.MinLength > MinLength)
        {
            MinLength = other.MinLength;
        }

        RequireUpper = (other.RequireUpper ?? false) || (RequireUpper ?? false);
        RequireNumbers = (other.RequireNumbers ?? false) || (RequireNumbers ?? false);
        RequireSpecial = (other.RequireSpecial ?? false) || (RequireNumbers ?? false);
        EnforceOnLogin = (other.EnforceOnLogin ?? false) || (RequireNumbers ?? false);
    }
}
