#nullable enable

using System.Text.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Dirt.Models.Response;

public class OrganizationIntegrationResponseModelTests
{
    [Theory, BitAutoData]
    public void Status_CloudBillingSync_AlwaysNotApplicable(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.CloudBillingSync;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);
        Assert.Equal(OrganizationIntegrationStatus.NotApplicable, model.Status);

        model.Configuration = "{}";
        Assert.Equal(OrganizationIntegrationStatus.NotApplicable, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Scim_AlwaysNotApplicable(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Scim;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);
        Assert.Equal(OrganizationIntegrationStatus.NotApplicable, model.Status);

        model.Configuration = "{}";
        Assert.Equal(OrganizationIntegrationStatus.NotApplicable, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Slack_NullConfig_ReturnsInitiated(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Slack;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Initiated, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Slack_WithConfig_ReturnsCompleted(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Slack;
        oi.Configuration = "{}";

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Completed, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Teams_NullConfig_ReturnsInitiated(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Teams;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Initiated, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Teams_WithTenantAndTeamsConfig_ReturnsInProgress(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Teams;
        oi.Configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: "tenant", Teams: [new TeamInfo() { DisplayName = "Team", Id = "TeamId", TenantId = "tenant" }]
        ));

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.InProgress, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Teams_WithCompletedConfig_ReturnsCompleted(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Teams;
        oi.Configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: "tenant",
            Teams: [new TeamInfo() { DisplayName = "Team", Id = "TeamId", TenantId = "tenant" }],
            ServiceUrl: new Uri("https://example.com"),
            ChannelId: "channellId"
        ));

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Completed, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Webhook_AlwaysCompleted(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Webhook;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);
        Assert.Equal(OrganizationIntegrationStatus.Completed, model.Status);

        model.Configuration = "{}";
        Assert.Equal(OrganizationIntegrationStatus.Completed, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Hec_NullConfig_ReturnsInvalid(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Hec;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Invalid, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Hec_WithConfig_ReturnsCompleted(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Hec;
        oi.Configuration = "{}";

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Completed, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Datadog_NullConfig_ReturnsInvalid(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Datadog;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Invalid, model.Status);
    }

    [Theory, BitAutoData]
    public void Status_Datadog_WithConfig_ReturnsCompleted(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Datadog;
        oi.Configuration = "{}";

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.Completed, model.Status);
    }
}
