using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class UriMatchDefaultsPolicyData : IPolicyDataModel
{
    /// <summary>
    /// The default URI match type for new cipher logins.
    /// </summary>
    [Display(Name = "DefaultUriMatchType")]
    public UriMatchType DefaultUriMatchType { get; set; } = UriMatchType.Domain; // Default to Base Domain

    /// <summary>
    /// Whether users can override the default URI match type for individual URIs
    /// </summary>
    [Display(Name = "AllowUserOverride")]
    public bool AllowUserOverride { get; set; } = true;
}
