using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Test.Models.Business;

/// <summary>
/// Contains test data for OrganizationLicense tests, including json strings for each OrganizationLicense version.
/// If you increment the OrganizationLicense version (e.g. because you've added a property to it), you must add the
/// json string for your new version to the LicenseVersions dictionary in this class.
/// See OrganizationLicenseTests.GenerateLicenseFileJsonString to help you do this.
/// </summary>
public static class OrganizationLicenseFileFixtures
{
    public const string InstallationId = "78900000-0000-0000-0000-000000000123";

    private const string Version12 =
        "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 11,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 11,\n  'Issued': '2023-11-23T03:15:41.632267Z',\n  'Refresh': '2023-11-30T03:15:41.632267Z',\n  'Expires': '2023-11-30T03:15:41.632267Z',\n  'ExpirationWithoutGracePeriod': null,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': 'eMSljdMAlFiiVYP/DI8LwNtSZZy6cJaC\\u002BAdmYGd1RTs=',\n  'Signature': ''\n}";

    private const string Version13 =
        "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 11,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 12,\n  'Issued': '2023-11-23T03:25:24.265409Z',\n  'Refresh': '2023-11-30T03:25:24.265409Z',\n  'Expires': '2023-11-30T03:25:24.265409Z',\n  'ExpirationWithoutGracePeriod': null,\n  'UsePasswordManager': true,\n  'UseSecretsManager': true,\n  'SmSeats': 5,\n  'SmServiceAccounts': 8,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': 'hZ4WcSX/7ooRZ6asDRMJ/t0K5hZkQdvkgEyy6wY\\u002BwQk=',\n  'Signature': ''\n}";

    private const string Version14 =
        "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 11,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 13,\n  'Issued': '2023-11-29T22:42:33.970597Z',\n  'Refresh': '2023-12-06T22:42:33.970597Z',\n  'Expires': '2023-12-06T22:42:33.970597Z',\n  'ExpirationWithoutGracePeriod': null,\n  'UsePasswordManager': true,\n  'UseSecretsManager': true,\n  'SmSeats': 5,\n  'SmServiceAccounts': 8,\n  'LimitCollectionCreationDeletion': true,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': '4G2u\\u002BWKO9EOiVnDVNr7uPxxRkv7TtaOmDl7kAYH05Fw=',\n  'Signature': ''\n}";

    private const string Version15 =
        "{\n  'LicenseKey': 'myLicenseKey',\n  'InstallationId': '78900000-0000-0000-0000-000000000123',\n  'Id': '12300000-0000-0000-0000-000000000456',\n  'Name': 'myOrg',\n  'BillingEmail': 'myBillingEmail',\n  'BusinessName': 'myBusinessName',\n  'Enabled': true,\n  'Plan': 'myPlan',\n  'PlanType': 11,\n  'Seats': 10,\n  'MaxCollections': 2,\n  'UsePolicies': true,\n  'UseSso': true,\n  'UseKeyConnector': true,\n  'UseScim': true,\n  'UseGroups': true,\n  'UseEvents': true,\n  'UseDirectory': true,\n  'UseTotp': true,\n  'Use2fa': true,\n  'UseApi': true,\n  'UseResetPassword': true,\n  'MaxStorageGb': 100,\n  'SelfHost': true,\n  'UsersGetPremium': true,\n  'UseCustomPermissions': true,\n  'Version': 14,\n  'Issued': '2023-12-14T02:03:33.374297Z',\n  'Refresh': '2023-12-07T22:42:33.970597Z',\n  'Expires': '2023-12-21T02:03:33.374297Z',\n  'ExpirationWithoutGracePeriod': null,\n  'UsePasswordManager': true,\n  'UseSecretsManager': true,\n  'SmSeats': 5,\n  'SmServiceAccounts': 8,\n  'LimitCollectionCreationDeletion': true,\n  'AllowAdminAccessToAllCollectionItems': true,\n  'Trial': true,\n  'LicenseType': 1,\n  'Hash': 'EZl4IvJaa1E5mPmlfp4p5twAtlmaxlF1yoZzVYP4vog=',\n  'Signature': ''\n}";

    private static readonly Dictionary<int, string> LicenseVersions = new()
    {
        { 12, Version12 },
        { 13, Version13 },
        { 14, Version14 },
        { 15, Version15 },
    };

    public static OrganizationLicense GetVersion(int licenseVersion)
    {
        if (!LicenseVersions.ContainsKey(licenseVersion))
        {
            throw new Exception(
                $"Cannot find serialized license version {licenseVersion}. You must add this to OrganizationLicenseFileFixtures when adding a new license version."
            );
        }

        var json = LicenseVersions.GetValueOrDefault(licenseVersion).Replace("'", "\"");
        var license = JsonSerializer.Deserialize<OrganizationLicense>(json);

        if (license.Version != licenseVersion - 1)
        {
            // license.Version is 1 behind. e.g. if we requested version 13, then license.Version == 12. If not,
            // the json string is probably for a different version and won't give us accurate test results.
            throw new Exception(
                $"License version {licenseVersion} in OrganizationLicenseFileFixtures did not match the expected version number. Make sure the json string is correct."
            );
        }

        return license;
    }

    /// <summary>
    /// The organization used to generate the license file json strings in this class.
    /// All its properties should be initialized with literal, non-default values.
    /// If you add an Organization property value, please add a value here as well.
    /// </summary>
    public static Organization OrganizationFactory() =>
        new()
        {
            Id = new Guid("12300000-0000-0000-0000-000000000456"),
            Identifier = "myIdentifier",
            Name = "myOrg",
            BusinessName = "myBusinessName",
            BusinessAddress1 = "myBusinessAddress1",
            BusinessAddress2 = "myBusinessAddress2",
            BusinessAddress3 = "myBusinessAddress3",
            BusinessCountry = "myBusinessCountry",
            BusinessTaxNumber = "myBusinessTaxNumber",
            BillingEmail = "myBillingEmail",
            Plan = "myPlan",
            PlanType = PlanType.EnterpriseAnnually2020,
            Seats = 10,
            MaxCollections = 2,
            UsePolicies = true,
            UseSso = true,
            UseKeyConnector = true,
            UseScim = true,
            UseGroups = true,
            UseDirectory = true,
            UseEvents = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            UseResetPassword = true,
            UseSecretsManager = true,
            SelfHost = true,
            UsersGetPremium = true,
            UseCustomPermissions = true,
            Storage = 100000,
            MaxStorageGb = 100,
            Gateway = GatewayType.Stripe,
            GatewayCustomerId = "myGatewayCustomerId",
            GatewaySubscriptionId = "myGatewaySubscriptionId",
            ReferenceData = "myReferenceData",
            Enabled = true,
            LicenseKey = "myLicenseKey",
            PublicKey = "myPublicKey",
            PrivateKey = "myPrivateKey",
            TwoFactorProviders = "myTwoFactorProviders",
            ExpirationDate = new DateTime(2024, 12, 24),
            CreationDate = new DateTime(2022, 10, 22),
            RevisionDate = new DateTime(2023, 11, 23),
            MaxAutoscaleSeats = 100,
            OwnersNotifiedOfAutoscaling = new DateTime(2020, 5, 10),
            Status = OrganizationStatusType.Created,
            UsePasswordManager = true,
            SmSeats = 5,
            SmServiceAccounts = 8,
            MaxAutoscaleSmSeats = 101,
            MaxAutoscaleSmServiceAccounts = 102,
            LimitCollectionCreation = true,
            LimitCollectionDeletion = true,
            AllowAdminAccessToAllCollectionItems = true,
        };
}
