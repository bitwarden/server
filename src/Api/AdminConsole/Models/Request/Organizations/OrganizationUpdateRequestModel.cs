using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUpdateRequestModel
{
    [Required]
    [StringLength(50, ErrorMessage = "The field Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }
    [StringLength(50, ErrorMessage = "The field Business Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string BusinessName { get; set; }
    [EmailAddress]
    [Required]
    [StringLength(256)]
    public string BillingEmail { get; set; }
    public Permissions Permissions { get; set; }
    public OrganizationKeysRequestModel Keys { get; set; }

    public virtual Organization ToOrganization(Organization existingOrganization, GlobalSettings globalSettings)
    {
        if (!globalSettings.SelfHosted)
        {
            // These items come from the license file
            existingOrganization.Name = Name;
            existingOrganization.BusinessName = BusinessName;
            existingOrganization.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
        }
        Keys?.ToOrganization(existingOrganization);
        return existingOrganization;
    }
}
