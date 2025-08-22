// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class SavePolicyRequest
{
    [Required]
    public PolicyRequestModel Policy { get; set; }

    public Dictionary<string, object> Metadata { get; set; }

    public async Task<SavePolicyModel> ToSavePolicyModelAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));

        var updatedPolicy = new PolicyUpdate()
        {
            Type = Policy.Type!.Value,
            OrganizationId = organizationId,
            Data = Policy.Data != null ? JsonSerializer.Serialize(Policy.Data) : null,
            Enabled = Policy.Enabled.GetValueOrDefault(),
        };

        var metadata = MapToPolicyMetadata();

        return new SavePolicyModel(updatedPolicy, performedBy, metadata);
    }

    private IPolicyMetadataModel MapToPolicyMetadata()
    {
        if (Metadata == null)
        {
            return new EmptyMetadataModel();
        }

        return Policy.Type switch
        {
            PolicyType.OrganizationDataOwnership => MapToPolicyMetadata<OrganizationModelOwnershipPolicyModel>(),
            PolicyType.TwoFactorAuthentication => new EmptyMetadataModel(),
            PolicyType.MasterPassword => new EmptyMetadataModel(),
            PolicyType.PasswordGenerator => new EmptyMetadataModel(),
            PolicyType.SingleOrg => new EmptyMetadataModel(),
            PolicyType.RequireSso => new EmptyMetadataModel(),
            PolicyType.DisableSend => new EmptyMetadataModel(),
            PolicyType.SendOptions => new EmptyMetadataModel(),
            PolicyType.ResetPassword => new EmptyMetadataModel(),
            PolicyType.MaximumVaultTimeout => new EmptyMetadataModel(),
            PolicyType.DisablePersonalVaultExport => new EmptyMetadataModel(),
            PolicyType.ActivateAutofill => new EmptyMetadataModel(),
            PolicyType.AutomaticAppLogIn => new EmptyMetadataModel(),
            PolicyType.FreeFamiliesSponsorshipPolicy => new EmptyMetadataModel(),
            PolicyType.RemoveUnlockWithPin => new EmptyMetadataModel(),
            PolicyType.RestrictedItemTypesPolicy => new EmptyMetadataModel(),
            _ => new EmptyMetadataModel()
        };
    }

    private IPolicyMetadataModel MapToPolicyMetadata<T>() where T : IPolicyMetadataModel, new()
    {
        try
        {
            var json = JsonSerializer.Serialize(Metadata);
            return CoreHelpers.LoadClassFromJsonData<T>(json);
        }
        catch
        {
            return new EmptyMetadataModel();
        }
    }
}
