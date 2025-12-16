using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Update;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUpdateRequestModel
{
    [StringLength(50, ErrorMessage = "The field Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string? Name { get; set; }

    [EmailAddress]
    [StringLength(256)]
    public string? BillingEmail { get; set; }

    public OrganizationKeysRequestModel? Keys { get; set; }

    public OrganizationUpdateRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        BillingEmail = BillingEmail,
        Keys = Keys != null ? new OrganizationKeyPair
        {
            PublicKey = Keys.PublicKey,
            PrivateKey = Keys.EncryptedPrivateKey
        } : null
    };
}
