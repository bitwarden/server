using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Models.Business;

public class OrganizationLicenseTests
{
    /// <summary>
    /// Verifies that when the license file is loaded from disk using the current OrganizationLicense class,
    /// it matches the Organization it was generated for.
    /// This guards against the risk that properties added in later versions are accidentally included in the validation
    /// </summary>
    [Theory]
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion)] // Previous version (this property is 1 behind)
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion + 1)] // Current version
    public void OrganizationLicense_LoadedFromDisk_VerifyData_Passes(int licenseVersion, ClaimsPrincipal claimsPrincipal)
    {
        var license = OrganizationLicenseFileFixtures.GetVersion(licenseVersion);

        // These licenses will naturally expire over time, but we still want them to be able to test
        license.Expires = DateTime.MaxValue;

        var organization = OrganizationLicenseFileFixtures.OrganizationFactory();
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Returns(new GlobalSettings.InstallationSettings
        {
            Id = new Guid(OrganizationLicenseFileFixtures.InstallationId)
        });
        Assert.True(license.VerifyData(organization, claimsPrincipal, globalSettings));
    }

    /// <summary>
    /// Known good GetDataBytes output for hash data (forHash: true) for all OrganizationLicense versions.
    /// These values were verified to be correct on initial implementation and serve as regression baselines.
    /// NOTE: License versions are now frozen. Use the JWT Token property to add new claims instead of incrementing the version.
    /// </summary>
    private static readonly Dictionary<int, string> _knownGoodOrganizationLicenseHashData = new()
    {
        { 1, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|UseDirectory:true|UseGroups:true|UseTotp:true|Version:1" },
        { 2, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|UseDirectory:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:2" },
        { 3, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|UseDirectory:true|UseEvents:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:3" },
        { 4, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:4" },
        { 5, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:5" },
        { 6, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsePolicies:true|UsersGetPremium:true|UseTotp:true|Version:6" },
        { 7, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsePolicies:true|UsersGetPremium:true|UseSso:true|UseTotp:true|Version:7" },
        { 8, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseSso:true|UseTotp:true|Version:8" },
        { 9, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseSso:true|UseTotp:true|Version:9" },
        { 10, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSso:true|UseTotp:true|Version:10" },
        { 11, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSso:true|UseTotp:true|Version:11" },
        { 12, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSso:true|UseTotp:true|Version:12" },
        { 13, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:13" },
        { 14, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|LimitCollectionCreationDeletion:true|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:14" },
        { 15, "license:organization|AllowAdminAccessToAllCollectionItems:true|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|LimitCollectionCreationDeletion:true|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:15" },
        { 16, "license:organization|AllowAdminAccessToAllCollectionItems:true|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|LicenseKey:myLicenseKey|LimitCollectionCreationDeletion:true|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:16" }
    };

    /// <summary>
    /// Known good GetDataBytes output for signature data (forHash: false) for all OrganizationLicense versions.
    /// These values were verified to be correct on initial implementation and serve as regression baselines.
    /// NOTE: License versions are now frozen. Use the JWT Token property to add new claims instead of incrementing the version.
    /// </summary>
    private static readonly Dictionary<int, string> _knownGoodOrganizationLicenseSignatureData = new()
    {
        { 1, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:WSyM/Q+vgOuWeF6XBH+RSfUqvf7NDtP3fgNfcbXYqKc=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|UseDirectory:true|UseGroups:true|UseTotp:true|Version:1" },
        { 2, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:n4g3leUf/egbnKk+/VgkJTvdxw2YRH6/zGgx89h+J60=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|UseDirectory:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:2" },
        { 3, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:zDoMNV/c8YpUypc+FmBoPyj73qOsg4snsMOJDcKFp9k=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|UseDirectory:true|UseEvents:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:3" },
        { 4, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:Y2sP9phSZ9GqbCC+PMp1KdnUhjfNaqNg6uzfUydrKZM=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:4" },
        { 5, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:PudZKNV7YAWJogm8BJf3wZIL+lESf3qzV/pQlZPPJjY=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsersGetPremium:true|UseTotp:true|Version:5" },
        { 6, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:7SjSYQENeAW4pUnXtsPaux2uipIWNWJz9VIrNW2gVsI=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsePolicies:true|UsersGetPremium:true|UseTotp:true|Version:6" },
        { 7, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:ujf4/zlDXv1g6ktlk9XBj/u3BkRZG+p5I00piGDiWp8=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsePolicies:true|UsersGetPremium:true|UseSso:true|UseTotp:true|Version:7" },
        { 8, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:GEM3AyWbQknnlDtoxyhw0QK7edYS2C/bffX5+p4G9ig=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseSso:true|UseTotp:true|Version:8" },
        { 9, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:5SF14wtEieiA9hjj+BTcrggHcx7dLEGbH+HLksvK79o=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseSso:true|UseTotp:true|Version:9" },
        { 10, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:NmbIpfiZUNxSvwbaolbUmItQCHIcVCTjfraR/NBlmvE=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSso:true|UseTotp:true|Version:10" },
        { 11, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|Expires:1740787200|Hash:dGZBQT/PORsuT/W2oRrngcjTboTyfZZVpDZBHshVK6Y=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSso:true|UseTotp:true|Version:11" },
        { 12, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Hash:rWecCXB0kuqi/RW3C8u2rLZRDMR49W3W4Q3eL2tZ3j8=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSso:true|UseTotp:true|Version:12" },
        { 13, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Hash:15fwM5v5Ba+t7JlD4ToYvtZmAoShWC3DrOD0lM5kXGE=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:13" },
        { 14, "license:organization|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Hash:2bTNBiH2G/Nzv6UVD1BNJQBGjT9et0UO8ComQofS8uo=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|LimitCollectionCreationDeletion:true|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:14" },
        { 15, "license:organization|AllowAdminAccessToAllCollectionItems:true|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Hash:3VjOyWJu38N4epIzhDzjRR80zQ651wnYkQCd+DIzeAs=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|LimitCollectionCreationDeletion:true|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:15" },
        { 16, "license:organization|AllowAdminAccessToAllCollectionItems:true|BillingEmail:myBillingEmail|BusinessName:|Enabled:true|ExpirationWithoutGracePeriod:|Expires:1740787200|Hash:Oo5KFBoX8pMcklJ4oJAqgv77/WA8+gDPxq6+/Fjffwc=|Id:12300000-0000-0000-0000-000000000456|InstallationId:78900000-0000-0000-0000-000000000123|Issued:1758888001|LicenseKey:myLicenseKey|LimitCollectionCreationDeletion:true|MaxCollections:2|MaxStorageGb:100|Name:myOrg|Plan:myPlan|PlanType:11|Refresh:1761480001|Seats:10|SelfHost:true|SmSeats:5|SmServiceAccounts:8|Trial:false|Use2fa:true|UseApi:true|UseCustomPermissions:true|UseDirectory:true|UseEvents:true|UseGroups:true|UseKeyConnector:true|UsePasswordManager:true|UsePolicies:true|UseResetPassword:true|UsersGetPremium:true|UseScim:true|UseSecretsManager:true|UseSso:true|UseTotp:true|Version:16" }
    };

    /// <summary>
    /// Regression test that verifies GetDataBytes output for hash data (forHash: true) remains stable across all OrganizationLicense versions.
    /// This protects against accidental changes to the data format that would break backward compatibility.
    /// If this test fails, it means the hash data format has changed and existing licenses may no longer validate.
    /// </summary>
    [Fact]
    public void OrganizationLicense_GetDataBytes_HashData_AllVersions()
    {
        // Verify each version produces the expected hash data format
        for (var version = 1; version <= 16; version++)
        {
            var license = CreateDeterministicOrganizationLicense(version);
            var actualHashData = System.Text.Encoding.UTF8.GetString(license.GetDataBytes(forHash: true));
            Assert.Equal(_knownGoodOrganizationLicenseHashData[version], actualHashData);
        }
    }

    /// <summary>
    /// Regression test that verifies GetDataBytes output for signature data (forHash: false) remains stable across all OrganizationLicense versions.
    /// This protects against accidental changes to the data format that would break backward compatibility.
    /// If this test fails, it means the signature data format has changed and existing licenses may no longer validate.
    /// </summary>
    [Fact]
    public void OrganizationLicense_GetDataBytes_SignatureData_AllVersions()
    {
        // Verify each version produces the expected signature data format
        for (var version = 1; version <= 16; version++)
        {
            var license = CreateDeterministicOrganizationLicense(version);
            var actualSignatureData = System.Text.Encoding.UTF8.GetString(license.GetDataBytes(forHash: false));
            Assert.Equal(_knownGoodOrganizationLicenseSignatureData[version], actualSignatureData);
        }
    }

    /// <summary>
    /// Validates that the OrganizationLicense version remains frozen at version 15.
    /// License versions should no longer be incremented. Use the JWT Token property to add new claims instead.
    /// If this test fails, it means someone attempted to increment the license version, which is no longer allowed.
    /// </summary>
    [Fact]
    public void OrganizationLicense_CurrentVersion_ShouldRemainFrozen()
    {
        const int expectedVersion = 15;
        var actualVersion = OrganizationLicense.CurrentLicenseFileVersion;

        Assert.True(actualVersion == expectedVersion, $@"
ERROR: OrganizationLicense.CurrentLicenseFileVersion has been changed from {expectedVersion} to {actualVersion}

License versions are now frozen and should not be incremented.

Instead of incrementing the version:
- Use the JWT Token property to add new claims
- Add your new capabilities as claims in the Token
- This allows for more flexible licensing without breaking backward compatibility

If you believe you need to change the version for a valid reason, please discuss with the team first.
");
    }

    /// <summary>
    /// Creates a deterministic OrganizationLicense for testing hash values.
    /// All property values are fixed to ensure reproducible hashes.
    /// </summary>
    private static OrganizationLicense CreateDeterministicOrganizationLicense(int version)
    {
        var organization = CreateDeterministicOrganization();
        var subscriptionInfo = CreateDeterministicSubscriptionInfo();
        var installationId = new Guid("78900000-0000-0000-0000-000000000123");
        var mockLicensingService = CreateMockLicensingService();

        var license = new OrganizationLicense(organization, subscriptionInfo, installationId, mockLicensingService, version);

        // Override timestamps to deterministic values (constructor sets them to DateTime.UtcNow)
        license.Issued = new DateTime(2025, 9, 26, 12, 0, 1, DateTimeKind.Utc); // Corresponds to 1759501361 Unix timestamp
        license.Refresh = new DateTime(2025, 10, 26, 12, 0, 1, DateTimeKind.Utc); // Corresponds to 1762093361 Unix timestamp

        // Recalculate hash with the deterministic Issued/Refresh values
        license.Hash = Convert.ToBase64String(license.ComputeHash());
        license.Signature = Convert.ToBase64String(mockLicensingService.SignLicense(license));

        return license;
    }

    /// <summary>
    /// Creates an Organization with deterministic property values for reproducible testing.
    /// </summary>
    private static Organization CreateDeterministicOrganization()
    {
        return new Organization
        {
            Id = new Guid("12300000-0000-0000-0000-000000000456"),
            Identifier = "myIdentifier",
            Name = "myOrg",
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
            UseEvents = true,
            UseDirectory = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            UseResetPassword = true,
            MaxStorageGb = 100,
            SelfHost = true,
            UsersGetPremium = true,
            UseCustomPermissions = true,
            Enabled = true,
            LicenseKey = "myLicenseKey",
            UsePasswordManager = true,
            UseSecretsManager = true,
            SmSeats = 5,
            SmServiceAccounts = 8,
            UseRiskInsights = false,
            LimitCollectionCreation = true,
            LimitCollectionDeletion = true,
            AllowAdminAccessToAllCollectionItems = true,
            UseOrganizationDomains = true,
            UseAdminSponsoredFamilies = false
        };
    }

    /// <summary>
    /// Creates a SubscriptionInfo with deterministic dates for reproducible testing.
    /// </summary>
    private static SubscriptionInfo CreateDeterministicSubscriptionInfo()
    {
        var stripeSubscription = new Subscription
        {
            Status = "active",
            TrialStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TrialEnd = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Items = new StripeList<SubscriptionItem>
            {
                Data = [
                    new SubscriptionItem
                    {
                        CurrentPeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        CurrentPeriodEnd = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            }
        };

        return new SubscriptionInfo
        {
            UpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice
            {
                Date = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
            },
            Subscription = new SubscriptionInfo.BillingSubscription(stripeSubscription)
        };
    }

    /// <summary>
    /// Creates a mock ILicensingService that returns a deterministic signature.
    /// </summary>
    private static ILicensingService CreateMockLicensingService()
    {
        var mockService = Substitute.For<ILicensingService>();
        mockService.SignLicense(Arg.Any<ILicense>())
            .Returns([0x00, 0x01, 0x02, 0x03]); // Dummy signature for hash testing
        return mockService;
    }
}
