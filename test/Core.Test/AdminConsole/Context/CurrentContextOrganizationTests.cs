using Bit.Core.Context;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Context;

public class CurrentContextOrganizationTests
{
    [Theory]
    // Access is only granted when the member holds it, the organization has the feature, and the organization is enabled.
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void Constructor_AccessSecretsManager_RequiresMemberAccessAndEnabledOrganization(
        bool accessSecretsManager, bool useSecretsManager, bool enabled, bool expected)
    {
        var orgUser = new OrganizationUserOrganizationDetails
        {
            OrganizationId = Guid.NewGuid(),
            Enabled = enabled,
            AccessSecretsManager = accessSecretsManager,
            UseSecretsManager = useSecretsManager
        };

        var sut = new CurrentContextOrganization(orgUser);

        Assert.Equal(expected, sut.AccessSecretsManager);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void Constructor_AccessPam_RequiresMemberAccessAndEnabledOrganization(
        bool accessPam, bool usePam, bool enabled, bool expected)
    {
        var orgUser = new OrganizationUserOrganizationDetails
        {
            OrganizationId = Guid.NewGuid(),
            Enabled = enabled,
            AccessPam = accessPam,
            UsePam = usePam
        };

        var sut = new CurrentContextOrganization(orgUser);

        Assert.Equal(expected, sut.AccessPam);
    }
}
