#nullable enable

using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response.Organizations;

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
    public void Status_Hec_NullConfig_ReturnsNotApplicable(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Hec;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.NotApplicable, model.Status);
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
    public void Status_Datadog_NullConfig_ReturnsNotApplicable(OrganizationIntegration oi)
    {
        oi.Type = IntegrationType.Datadog;
        oi.Configuration = null;

        var model = new OrganizationIntegrationResponseModel(oi);

        Assert.Equal(OrganizationIntegrationStatus.NotApplicable, model.Status);
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
