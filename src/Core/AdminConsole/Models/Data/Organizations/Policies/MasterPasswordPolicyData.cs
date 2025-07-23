﻿using System.Text.Json.Serialization;
namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class MasterPasswordPolicyData : IPolicyDataModel
{
    [JsonPropertyName("minComplexity")]
    public int? MinComplexity { get; set; }
    [JsonPropertyName("minLength")]
    public int? MinLength { get; set; }
    [JsonPropertyName("requireLower")]
    public bool? RequireLower { get; set; }
    [JsonPropertyName("requireUpper")]
    public bool? RequireUpper { get; set; }
    [JsonPropertyName("requireNumbers")]
    public bool? RequireNumbers { get; set; }
    [JsonPropertyName("requireSpecial")]
    public bool? RequireSpecial { get; set; }
    [JsonPropertyName("enforceOnLogin")]
    public bool? EnforceOnLogin { get; set; }

    /// <summary>
    /// Combine the other policy data with this instance, taking the most secure options
    /// </summary>
    /// <param name="other">The other policy instance to combine with this</param>
    public void CombineWith(MasterPasswordPolicyData? other)
    {
        if (other == null)
        {
            return;
        }

        if (other.MinComplexity.HasValue && (!MinComplexity.HasValue || other.MinComplexity > MinComplexity))
        {
            MinComplexity = other.MinComplexity;
        }

        if (other.MinLength.HasValue && (!MinLength.HasValue || other.MinLength > MinLength))
        {
            MinLength = other.MinLength;
        }

        RequireLower = (other.RequireLower ?? false) || (RequireLower ?? false);
        RequireUpper = (other.RequireUpper ?? false) || (RequireUpper ?? false);
        RequireNumbers = (other.RequireNumbers ?? false) || (RequireNumbers ?? false);
        RequireSpecial = (other.RequireSpecial ?? false) || (RequireSpecial ?? false);
        EnforceOnLogin = (other.EnforceOnLogin ?? false) || (EnforceOnLogin ?? false);
    }
}
