using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Entities;

public class OrganizationConnectionTests
{
    [Theory]
    [BitAutoData]
    public void OrganizationConnection_CanUse_Success(Guid connectionId, Guid organizationId)
    {
        var connection = new OrganizationConnection<ScimConfig>()
        {
            Id = connectionId,
            OrganizationId = organizationId,
            Enabled = true,
            Type = OrganizationConnectionType.Scim,
            Config = new ScimConfig() { Enabled = true }
        };

        Assert.True(connection.Validate<ScimConfig>(out var exception));
        Assert.True(string.IsNullOrEmpty(exception));
    }

    [Theory]
    [BitAutoData]
    public void OrganizationConnection_CanUse_WhenDisabled_ReturnsFalse(Guid connectionId, Guid organizationId)
    {

        var connection = new OrganizationConnection<ScimConfig>()
        {
            Id = connectionId,
            OrganizationId = organizationId,
            Enabled = false,
            Type = OrganizationConnectionType.Scim,
            Config = new ScimConfig() { Enabled = true }
        };

        Assert.False(connection.Validate<ScimConfig>(out var exception));
        Assert.Contains("Connection disabled", exception);
    }

    [Theory]
    [BitAutoData]
    public void OrganizationConnection_CanUse_WhenNoConfig_ReturnsFalse(Guid connectionId, Guid organizationId)
    {
        var connection = new OrganizationConnection<ScimConfig>()
        {
            Id = connectionId,
            OrganizationId = organizationId,
            Enabled = true,
            Type = OrganizationConnectionType.Scim,
        };

        Assert.False(connection.Validate<ScimConfig>(out var exception));
        Assert.Contains("No saved Connection config", exception);
    }

    [Theory]
    [BitAutoData]
    public void OrganizationConnection_CanUse_WhenConfigInvalid_ReturnsFalse(Guid connectionId, Guid organizationId)
    {
        var connection = new OrganizationConnection<ScimConfig>()
        {
            Id = connectionId,
            OrganizationId = organizationId,
            Enabled = true,
            Type = OrganizationConnectionType.Scim,
            Config = new ScimConfig() { Enabled = false }
        };

        Assert.False(connection.Validate<ScimConfig>(out var exception));
        Assert.Contains("Scim Config is disabled", exception);
    }
}
