using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Billing.Organizations.Models;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Entities;

public class OrganizationTests
{
    private static readonly Dictionary<TwoFactorProviderType, TwoFactorProvider> _testConfig = new Dictionary<TwoFactorProviderType, TwoFactorProvider>()
    {
        [TwoFactorProviderType.OrganizationDuo] = new TwoFactorProvider
        {
            Enabled = true,
            MetaData = new Dictionary<string, object>
            {
                ["IKey"] = "IKey_value",
                ["SKey"] = "SKey_value",
                ["Host"] = "Host_value",
            },
        }
    };


    [Fact]
    public void SetTwoFactorProviders_Success()
    {
        var organization = new Organization();
        organization.SetTwoFactorProviders(_testConfig);

        using var jsonDocument = JsonDocument.Parse(organization.TwoFactorProviders);
        var root = jsonDocument.RootElement;

        var duo = AssertHelper.AssertJsonProperty(root, "6", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(duo, "Enabled", JsonValueKind.True);
        var duoMetaData = AssertHelper.AssertJsonProperty(duo, "MetaData", JsonValueKind.Object);
        var iKey = AssertHelper.AssertJsonProperty(duoMetaData, "IKey", JsonValueKind.String).GetString();
        Assert.Equal("IKey_value", iKey);
        var sKey = AssertHelper.AssertJsonProperty(duoMetaData, "SKey", JsonValueKind.String).GetString();
        Assert.Equal("SKey_value", sKey);
        var host = AssertHelper.AssertJsonProperty(duoMetaData, "Host", JsonValueKind.String).GetString();
        Assert.Equal("Host_value", host);
    }

    [Fact]
    public void GetTwoFactorProviders_Success()
    {
        // This is to get rid of the cached dictionary the SetTwoFactorProviders keeps so we can fully test the JSON reading
        // It intent is to mimic a storing of the entity in the database and it being read later
        var tempOrganization = new Organization();
        tempOrganization.SetTwoFactorProviders(_testConfig);
        var organization = new Organization
        {
            TwoFactorProviders = tempOrganization.TwoFactorProviders,
        };

        var twoFactorProviders = organization.GetTwoFactorProviders();

        var duo = Assert.Contains(TwoFactorProviderType.OrganizationDuo, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.True(duo.Enabled);
        Assert.NotNull(duo.MetaData);
        var iKey = Assert.Contains("IKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("IKey_value", iKey);
        var sKey = Assert.Contains("SKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("SKey_value", sKey);
        var host = Assert.Contains("Host", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("Host_value", host);
    }

    [Fact]
    public void GetTwoFactorProviders_SavedWithName_Success()
    {
        var organization = new Organization();
        // This should save items with the string name of the enum and we will validate that we can read
        // from that just incase some organizations have it saved that way.
        organization.TwoFactorProviders = JsonSerializer.Serialize(_testConfig);

        // Preliminary Asserts to make sure we are testing what we want to be testing
        using var jsonDocument = JsonDocument.Parse(organization.TwoFactorProviders);
        var root = jsonDocument.RootElement;
        // This means it saved the enum as its string name
        AssertHelper.AssertJsonProperty(root, "OrganizationDuo", JsonValueKind.Object);

        // Actual checks
        var twoFactorProviders = organization.GetTwoFactorProviders();

        var duo = Assert.Contains(TwoFactorProviderType.OrganizationDuo, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.True(duo.Enabled);
        Assert.NotNull(duo.MetaData);
        var iKey = Assert.Contains("IKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("IKey_value", iKey);
        var sKey = Assert.Contains("SKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("SKey_value", sKey);
        var host = Assert.Contains("Host", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("Host_value", host);
    }

    [Fact]
    public void UseDisableSmAdsForUsers_DefaultValue_IsFalse()
    {
        var organization = new Organization();

        Assert.False(organization.UseDisableSmAdsForUsers);
    }

    [Fact]
    public void UseDisableSmAdsForUsers_CanBeSetToTrue()
    {
        var organization = new Organization
        {
            UseDisableSmAdsForUsers = true
        };

        Assert.True(organization.UseDisableSmAdsForUsers);
    }

    [Fact]
    public void UpdateFromLicense_AppliesAllLicenseProperties()
    {
        // This test ensures that when a new property is added to OrganizationLicense,
        // it is also applied to the Organization in UpdateFromLicense().
        // This is the fourth step in the license synchronization pipeline:
        // Property → Constant → Claim → Extraction → Application

        // 1. Get all public properties from OrganizationLicense
        var licenseProperties = typeof(OrganizationLicense)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        // 2. Define properties that don't need to be applied to Organization
        var excludedProperties = new HashSet<string>
        {
            // Internal/computed properties
            "SignatureBytes",             // Computed from Signature property
            "ValidLicenseVersion",        // Internal property, not serialized
            "CurrentLicenseFileVersion",  // Constant field, not an instance property
            "Hash",                       // Signature-related, not applied to org
            "Signature",                  // Signature-related, not applied to org
            "Token",                      // The JWT itself, not applied to org
            "Version",                    // License version, not stored on org

            // Properties intentionally excluded from UpdateFromLicense
            "Id",                         // Self-hosted org has its own unique Guid
            "MaxStorageGb",               // Not enforced for self-hosted (per comment in UpdateFromLicense)

            // Properties not stored on Organization model
            "LicenseType",                // Not a property on Organization
            "InstallationId",             // Not a property on Organization
            "Issued",                     // Not a property on Organization
            "Refresh",                    // Not a property on Organization
            "ExpirationWithoutGracePeriod", // Not a property on Organization
            "Trial",                      // Not a property on Organization
            "Expires",                    // Mapped to ExpirationDate on Organization (different name)

            // Deprecated properties not applied
            "LimitCollectionCreationDeletion",      // Deprecated, not applied
            "AllowAdminAccessToAllCollectionItems", // Deprecated, not applied
        };

        // 3. Get properties that should be applied
        var propertiesThatShouldBeApplied = licenseProperties
            .Except(excludedProperties)
            .ToHashSet();

        // 4. Read Organization.UpdateFromLicense source code
        var organizationSourcePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "src", "Core", "AdminConsole", "Entities", "Organization.cs");
        var sourceCode = File.ReadAllText(organizationSourcePath);

        // 5. Find all property assignments in UpdateFromLicense method
        // Pattern matches: PropertyName = license.PropertyName
        // This regex looks for assignments like "Name = license.Name" or "ExpirationDate = license.Expires"
        var assignmentPattern = @"(\w+)\s*=\s*license\.(\w+)";
        var matches = Regex.Matches(sourceCode, assignmentPattern);

        var appliedProperties = new HashSet<string>();
        foreach (Match match in matches)
        {
            // Get the license property name (right side of assignment)
            var licensePropertyName = match.Groups[2].Value;
            appliedProperties.Add(licensePropertyName);
        }

        // Special case: Expires is mapped to ExpirationDate
        if (appliedProperties.Contains("Expires"))
        {
            appliedProperties.Add("Expires"); // Already added, but being explicit
        }

        // 6. Find missing applications
        var missingApplications = propertiesThatShouldBeApplied
            .Except(appliedProperties)
            .OrderBy(p => p)
            .ToList();

        // 7. Build error message with guidance
        var errorMessage = "";
        if (missingApplications.Any())
        {
            errorMessage = $"The following OrganizationLicense properties are NOT applied to Organization in UpdateFromLicense():\n";
            errorMessage += string.Join("\n", missingApplications.Select(p => $"  - {p}"));
            errorMessage += "\n\nPlease add the following lines to Organization.UpdateFromLicense():\n";
            foreach (var prop in missingApplications)
            {
                errorMessage += $"  {prop} = license.{prop};\n";
            }
            errorMessage += "\nNote: If the property maps to a different name on Organization (like Expires → ExpirationDate), adjust accordingly.";
        }

        // 8. Assert - if this fails, the error message guides the developer to add the application
        Assert.True(
            !missingApplications.Any(),
            $"\n{errorMessage}");
    }
}
