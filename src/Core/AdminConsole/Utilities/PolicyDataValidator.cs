using System.ComponentModel.DataAnnotations;
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
    /// <returns>Serialized JSON string if data is valid, null if data is null or empty</returns>
    /// <exception cref="BadRequestException">Thrown when data validation fails</exception>
    public static string? ValidateAndSerialize(Dictionary<string, object>? data, PolicyType policyType)
    {
        if (data == null || data.Count == 0)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);

            switch (policyType)
            {
                case PolicyType.MasterPassword:
                    var masterPasswordData = CoreHelpers.LoadClassFromJsonData<MasterPasswordPolicyData>(json);
                    ValidateModel(masterPasswordData, policyType);
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
        catch (JsonException ex)
        {
            var fieldName = !string.IsNullOrEmpty(ex.Path) ? ex.Path.TrimStart('$', '.') : null;
            var fieldInfo = !string.IsNullOrEmpty(fieldName) ? $": {fieldName} has an invalid value" : "";
            throw new BadRequestException($"Invalid data for {policyType} policy{fieldInfo}.");
        }
    }

    private static void ValidateModel(object model, PolicyType policyType)
    {
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(model, validationContext, validationResults, true))
        {
            var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
            throw new BadRequestException($"Invalid data for {policyType} policy: {errors}");
        }
    }

    /// <summary>
    /// Validates and deserializes policy metadata based on the policy type.
    /// </summary>
    /// <param name="metadata">The policy metadata to validate</param>
    /// <param name="policyType">The type of policy</param>
    /// <returns>Deserialized metadata model, or EmptyMetadataModel if metadata is null, empty, or validation fails</returns>
    public static IPolicyMetadataModel ValidateAndDeserializeMetadata(Dictionary<string, object>? metadata, PolicyType policyType)
    {
        if (metadata == null || metadata.Count == 0)
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
        catch (JsonException)
        {
            return new EmptyMetadataModel();
        }
    }
}
