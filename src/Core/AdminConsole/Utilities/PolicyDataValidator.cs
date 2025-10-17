using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Utilities;

public static class PolicyDataValidator
{
    /// <summary>
    /// Validates and serializes policy data based on the policy type.
    /// </summary>
    /// <param name="data">The policy data to validate</param>
    /// <param name="policyType">The type of policy</param>
    /// <returns>Serialized JSON string if data is valid, null if data is empty</returns>
    /// <exception cref="BadRequestException">Thrown when data validation fails</exception>
    public static string? ValidateAndSerialize(Dictionary<string, object> data, PolicyType policyType)
    {
        if (data.Count == 0)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);

            switch (policyType)
            {
                case PolicyType.MasterPassword:
                    CoreHelpers.LoadClassFromJsonData<MasterPasswordPolicyData>(json);
                    break;
                case PolicyType.SendOptions:
                    CoreHelpers.LoadClassFromJsonData<SendOptionsPolicyData>(json);
                    break;
                case PolicyType.ResetPassword:
                    CoreHelpers.LoadClassFromJsonData<ResetPasswordDataModel>(json);
                    break;
            }

            return json;
        }
        catch
        {
            throw new BadRequestException($"Invalid data for {policyType} policy.");
        }
    }

    /// <summary>
    /// Validates and deserializes policy metadata based on the policy type.
    /// </summary>
    /// <param name="metadata">The policy metadata to validate</param>
    /// <param name="policyType">The type of policy</param>
    /// <returns>Deserialized metadata model, or EmptyMetadataModel if metadata is empty, or validation fails</returns>
    public static IPolicyMetadataModel ValidateAndDeserializeMetadata(Dictionary<string, object> metadata, PolicyType policyType)
    {
        if (metadata.Count == 0)
        {
            return new EmptyMetadataModel();
        }

        try
        {
            var json = JsonSerializer.Serialize(metadata);

            return policyType switch
            {
                PolicyType.OrganizationDataOwnership =>
                    CoreHelpers.LoadClassFromJsonData<OrganizationModelOwnershipPolicyModel>(json),
                _ => new EmptyMetadataModel()
            };
        }
        catch
        {
            return new EmptyMetadataModel();
        }
    }
}
