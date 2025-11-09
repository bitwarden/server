using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUpdateRequestModel
{
    [StringLength(50, ErrorMessage = "The field Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public required string? Name { get; set; }

    [EmailAddress]
    [StringLength(256)]
    public required string? BillingEmail { get; set; }

    public OrganizationKeysRequestModel? Keys { get; set; }

    public UpdateOrganizationRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        BillingEmail = BillingEmail,
        PublicKey = Keys?.PublicKey,
        EncryptedPrivateKey = Keys?.EncryptedPrivateKey
    };
}
