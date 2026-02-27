using System.Text.Json.Serialization;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class OrganizationDataOwnershipPolicyData : IPolicyDataModel
{
    [JsonPropertyName("enableIndividualItemsTransfer")]
    public bool EnableIndividualItemsTransfer { get; set; }
}
