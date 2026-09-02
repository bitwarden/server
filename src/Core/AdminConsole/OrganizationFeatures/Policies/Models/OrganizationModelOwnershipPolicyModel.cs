
namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

public class OrganizationModelOwnershipPolicyModel : IPolicyMetadataModel
{
    public OrganizationModelOwnershipPolicyModel()
    {
    }

    public OrganizationModelOwnershipPolicyModel(string? defaultUserCollectionName)
    {
        DefaultUserCollectionName = defaultUserCollectionName;
    }

    public string? DefaultUserCollectionName { get; set; }
}
