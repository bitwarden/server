using System.Text.Json;
using Bit.Core.Models.Business;

namespace Bit.Core.Test.Models.Business;

public static class OrganizationLicenseStaticVersions
{
    public const string InstallationId = "78900000-0000-0000-0000-000000000123";

    private const string Version13 = "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 15,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 12,\n  'Issued': '2023-11-23T01:54:26.598514Z',\n  'Refresh': '2023-11-30T01:54:26.598514Z',\n  'Expires': '2023-11-30T01:54:26.598514Z',\n  'ExpirationWithoutGracePeriod': null,\n  'UsePasswordManager': true,\n  'UseSecretsManager': true,\n  'SmSeats': 5,\n  'SmServiceAccounts': 8,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': '4vUqpEranvb6nhX0d0tIOzSz\\u002B4bTE\\u002BT6hD5AbaJA9W4=',\n  'Signature': ''\n}";

    private static Dictionary<int, string> LicenseVersions = new() { { 13, Version13 } };

    public static OrganizationLicense GetVersion(int licenseVersion)
    {
        if (!LicenseVersions.ContainsKey(licenseVersion))
        {
            throw new Exception($"Cannot find serialized license version {licenseVersion}. You must add this to OrganizationLicenseStaticVersions when adding a new license version.");
        }

        var json = LicenseVersions.GetValueOrDefault(licenseVersion).Replace("'", "\"");
        return JsonSerializer.Deserialize<OrganizationLicense>(json);
    }
}
