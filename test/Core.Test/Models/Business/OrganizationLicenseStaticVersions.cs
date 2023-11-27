using System.Text.Json;
using Bit.Core.Models.Business;

namespace Bit.Core.Test.Models.Business;

public static class OrganizationLicenseStaticVersions
{
    public const string InstallationId = "78900000-0000-0000-0000-000000000123";

    private const string Version12 =
        "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 11,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 11,\n  'Issued': '2023-11-23T03:15:41.632267Z',\n  'Refresh': '2023-11-30T03:15:41.632267Z',\n  'Expires': '2023-11-30T03:15:41.632267Z',\n  'ExpirationWithoutGracePeriod': null,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': 'eMSljdMAlFiiVYP/DI8LwNtSZZy6cJaC\\u002BAdmYGd1RTs=',\n  'Signature': ''\n}";

    private const string Version13 =
        "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 11,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 12,\n  'Issued': '2023-11-23T03:25:24.265409Z',\n  'Refresh': '2023-11-30T03:25:24.265409Z',\n  'Expires': '2023-11-30T03:25:24.265409Z',\n  'ExpirationWithoutGracePeriod': null,\n  'UsePasswordManager': true,\n  'UseSecretsManager': true,\n  'SmSeats': 5,\n  'SmServiceAccounts': 8,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': 'hZ4WcSX/7ooRZ6asDRMJ/t0K5hZkQdvkgEyy6wY\\u002BwQk=',\n  'Signature': ''\n}";

    private static readonly Dictionary<int, string> LicenseVersions = new() { { 12, Version12 }, { 13, Version13 } };

    public static OrganizationLicense GetVersion(int licenseVersion)
    {
        if (!LicenseVersions.ContainsKey(licenseVersion))
        {
            throw new Exception(
                $"Cannot find serialized license version {licenseVersion}. You must add this to OrganizationLicenseStaticVersions when adding a new license version.");
        }

        var json = LicenseVersions.GetValueOrDefault(licenseVersion).Replace("'", "\"");
        var license = JsonSerializer.Deserialize<OrganizationLicense>(json);

        if (license.Version != licenseVersion - 1)
        {
            // license.Version is 1 behind. e.g. if we requested version 13, then license.Version == 12. If not,
            // the json string is probably for a different version and won't give us accurate test results.
            throw new Exception(
                $"License version {licenseVersion} in the static versions did not match the expected version number. Make sure the json string is correct.");
        }

        return license;
    }
}
