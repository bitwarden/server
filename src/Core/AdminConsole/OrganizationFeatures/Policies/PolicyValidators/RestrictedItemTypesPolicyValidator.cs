using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Vault.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class RestrictedItemTypesPolicyValidator : IPolicyValidator
{
    public PolicyType Type => PolicyType.RestrictedItemTypesPolicy;
    public IEnumerable<PolicyType> RequiredPolicies => Array.Empty<PolicyType>();

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        // No side effects needed for RestrictedItemTypes policy
        return Task.CompletedTask;
    }

    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (policyUpdate.Enabled && !string.IsNullOrEmpty(policyUpdate.Data))
        {
            try
            {
                var data = JsonSerializer.Deserialize<RestrictedItemTypesPolicyData>(policyUpdate.Data);

                if (data?.RestrictedItemTypes != null)
                {
                    // Validate that all cipher types are valid enum values
                    var validCipherTypes = Enum.GetValues<CipherType>();
                    var invalidTypes = data.RestrictedItemTypes.Where(type => !validCipherTypes.Contains(type)).ToList();

                    if (invalidTypes.Any())
                    {
                        return Task.FromResult($"Invalid cipher types: {string.Join(", ", invalidTypes)}");
                    }

                    // Validate that Login cipher type is not restricted (business rule)
                    if (data.RestrictedItemTypes.Contains(CipherType.Login))
                    {
                        return Task.FromResult("Login items cannot be restricted as they are essential for password management.");
                    }
                }
            }
            catch (JsonException)
            {
                return Task.FromResult("Invalid policy data format.");
            }
        }

        return Task.FromResult("");
    }
}
