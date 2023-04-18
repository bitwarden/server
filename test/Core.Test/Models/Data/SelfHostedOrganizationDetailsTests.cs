using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models.Data;

public class SelfHostedOrganizationDetailsTests
{
    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_OccupiedSeatCount_ExceedsLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.Seats = 1;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Remove some users", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_MaxCollections_ExceedsLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.MaxCollections = 1;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Remove some collections", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_Groups_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseGroups = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new license does not allow for the use of groups", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_Policies_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UsePolicies = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new license does not allow for the use of policies", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_DisabledPolicies_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UsePolicies = false;
        ((List<Policy>)orgDetails.Policies).ForEach(p => p.Enabled = false);

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_Sso_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseSso = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new license does not allow for the use of SSO", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_DisabledSso_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseSso = false;
        orgDetails.SsoConfig.Enabled = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_NoSso_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseSso = false;
        orgDetails.SsoConfig = null;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_KeyConnector_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseKeyConnector = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new license does not allow for the use of Key Connector", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_DisabledKeyConnector_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseKeyConnector = false;
        orgDetails.SsoConfig.SetData(new SsoConfigurationData() { KeyConnectorEnabled = false });

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_NoSsoKeyConnector_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseKeyConnector = false;
        orgDetails.SsoConfig = null;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_Scim_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseScim = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new plan does not allow the SCIM feature", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_DisabledScim_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseScim = false;
        ((List<OrganizationConnection<ScimConfig>>)orgDetails.ScimConnections)
            .ForEach(c => c.SetConfig(new ScimConfig() { Enabled = false }));

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_NoScimConfig_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseScim = false;
        orgDetails.ScimConnections = null;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_CustomPermissions_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseCustomPermissions = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new plan does not allow the Custom Permissions feature", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_NoCustomPermissions_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseCustomPermissions = false;
        ((List<OrganizationUser>)orgDetails.OrganizationUsers).ForEach(ou => ou.Type = OrganizationUserType.User);

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_ResetPassword_NotAllowedByLicense_Fail(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseResetPassword = false;

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.False(result);
        Assert.Contains("Your new license does not allow the Password Reset feature", exception);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public async Task ValidateForOrganization_DisabledResetPassword_NotAllowedByLicense_Success(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        var (orgDetails, orgLicense) = GetOrganizationAndLicense(orgUsers, policies, ssoConfig, scimConnections, license);
        orgLicense.UseResetPassword = false;
        ((List<Policy>)orgDetails.Policies).ForEach(p => p.Enabled = false);

        var result = orgDetails.CanUseLicense(license, out var exception);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(exception));
    }

    private (SelfHostedOrganizationDetails organization, OrganizationLicense license) GetOrganizationAndLicense(List<OrganizationUser> orgUsers,
        List<Policy> policies, SsoConfig ssoConfig, List<OrganizationConnection<ScimConfig>> scimConnections, OrganizationLicense license)
    {
        // The default state is that all features are used by Org and allowed by License
        // Each test then toggles on/off as necessary
        policies.ForEach(p => p.Enabled = true);
        policies.First().Type = PolicyType.ResetPassword;

        ssoConfig.Enabled = true;
        ssoConfig.SetData(new SsoConfigurationData()
        {
            KeyConnectorEnabled = true
        });

        var enabledScimConfig = new ScimConfig() { Enabled = true };
        scimConnections.ForEach(c => c.Config = enabledScimConfig);

        orgUsers.First().Type = OrganizationUserType.Custom;

        var organization = new SelfHostedOrganizationDetails()
        {
            OccupiedSeatCount = 10,
            CollectionCount = 5,
            GroupCount = 5,
            OrganizationUsers = orgUsers,
            Policies = policies,
            SsoConfig = ssoConfig,
            ScimConnections = scimConnections,

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
            SelfHost = true,
            UsersGetPremium = true,
            UseCustomPermissions = true,
        };

        license.Enabled = true;
        license.PlanType = PlanType.EnterpriseAnnually;
        license.Seats = 10;
        license.MaxCollections = 5;
        license.UsePolicies = true;
        license.UseSso = true;
        license.UseKeyConnector = true;
        license.UseScim = true;
        license.UseGroups = true;
        license.UseEvents = true;
        license.UseDirectory = true;
        license.UseTotp = true;
        license.Use2fa = true;
        license.UseApi = true;
        license.UseResetPassword = true;
        license.MaxStorageGb = 1;
        license.SelfHost = true;
        license.UsersGetPremium = true;
        license.UseCustomPermissions = true;
        license.Version = 11;
        license.Issued = DateTime.Now;
        license.Expires = DateTime.Now.AddYears(1);

        return (organization, license);
    }
}
