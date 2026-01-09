using System.Reflection;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Organizations.Models;
using Xunit;

namespace Bit.Core.Test.Billing.Licenses;

public class LicenseConstantsTests
{
    [Fact]
    public void OrganizationLicenseConstants_HasConstantForEveryLicenseProperty()
    {
        // This test ensures that when a new property is added to OrganizationLicense,
        // a corresponding constant is added to OrganizationLicenseConstants.
        // This is the first step in the license synchronization pipeline:
        // Property → Constant → Claim → Extraction → Application

        // 1. Get all public properties from OrganizationLicense
        var licenseProperties = typeof(OrganizationLicense)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        // 2. Get all constants from OrganizationLicenseConstants
        var constants = typeof(OrganizationLicenseConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null) as string)
            .ToHashSet();

        // 3. Define properties that don't need constants (internal/computed/non-claims properties)
        var excludedProperties = new HashSet<string>
        {
            "SignatureBytes",             // Computed from Signature property
            "ValidLicenseVersion",        // Internal property, not serialized
            "CurrentLicenseFileVersion",  // Constant field, not an instance property
            "Hash",                       // Signature-related, not in claims system
            "Signature",                  // Signature-related, not in claims system
            "Token",                      // The JWT itself, not a claim within the token
            "Version"                     // Not in claims system (only in deprecated property-based licenses)
        };

        // 4. Find license properties without corresponding constants
        var propertiesWithoutConstants = licenseProperties
            .Except(constants)
            .Except(excludedProperties)
            .OrderBy(p => p)
            .ToList();

        // 5. Build error message with guidance
        var errorMessage = "";
        if (propertiesWithoutConstants.Any())
        {
            errorMessage = $"The following OrganizationLicense properties don't have constants in OrganizationLicenseConstants:\n";
            errorMessage += string.Join("\n", propertiesWithoutConstants.Select(p => $"  - {p}"));
            errorMessage += "\n\nPlease add the following constants to OrganizationLicenseConstants:\n";
            foreach (var prop in propertiesWithoutConstants)
            {
                errorMessage += $"  public const string {prop} = nameof({prop});\n";
            }
        }

        // 6. Assert - if this fails, the error message guides the developer to add the constant
        Assert.True(
            !propertiesWithoutConstants.Any(),
            $"\n{errorMessage}");
    }
}
