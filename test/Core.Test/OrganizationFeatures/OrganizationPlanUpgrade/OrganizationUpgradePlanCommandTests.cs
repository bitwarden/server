using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Services;
using Bit.Core.Models.StaticStore;
using Bit.Core.Tools.Services;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationPlanUpgrade;

[SutProviderCustomize]

public class OrganizationUpgradePlanCommandTests
{

    [Theory]
    [BitAutoData]
    public async Task UpgradePlanAsync_ExistingSubscription_ThrowsBadRequestException(SutProvider<OrganizationUpgradePlanCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var upgrade = new OrganizationUpgrade
        {
            Plan = PlanType.EnterpriseAnnually,
        };
        var existingPlan = new Plan()
        {
            Type = PlanType.TeamsAnnually,
            BitwardenProduct = BitwardenProductType.PasswordManager,
        };

        var newPlans = new List<Plan>
        {
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BitwardenProduct = BitwardenProductType.PasswordManager,

            },
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BaseServiceAccount = 2,
                BitwardenProduct = BitwardenProductType.SecretsManager,

            }
        };

        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = "gateway-customer-id",
            GatewaySubscriptionId = "existing-subscription-id",
            PlanType = PlanType.Free,
        };

        var organizationUpgradeQuery = sutProvider.GetDependency<IOrganizationUpgradeQuery>();
        organizationUpgradeQuery.GetOrgById(organizationId).Returns(organization);
        organizationUpgradeQuery.ExistingPlan(PlanType.TeamsAnnually).Returns(existingPlan);
        organizationUpgradeQuery.NewPlans(PlanType.EnterpriseAnnually).Returns(newPlans);

        var validateUpgradeCommand = sutProvider.GetDependency<IValidateUpgradeCommand>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var referenceEventService = sutProvider.GetDependency<IReferenceEventService>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var paymentService = sutProvider.GetDependency<IPaymentService>();
        var organizationSignUpValidationStrategy = sutProvider.GetDependency<IOrganizationSignUpValidationStrategy>();

        var command = new OrganizationUpgradePlanCommand(
            organizationUpgradeQuery,
            validateUpgradeCommand,
            organizationService,
            referenceEventService,
            currentContext,
            paymentService,
            organizationSignUpValidationStrategy
        );

        await Assert.ThrowsAsync<BadRequestException>(() => command.UpgradePlanAsync(organizationId, upgrade));
    }

    [Theory]
    [BitAutoData]
    public async Task UpgradePlanAsync_NoPaymentMethodAvailable_ThrowsBadRequestException(SutProvider<OrganizationUpgradePlanCommand> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        var upgrade = new OrganizationUpgrade
        {
            Plan = PlanType.EnterpriseAnnually,
        };

        var organization = new Organization
        {
            Id = organizationId,
            GatewayCustomerId = null,
        };

        var existingPlan = new Plan()
        {
            Type = PlanType.TeamsAnnually,
            BitwardenProduct = BitwardenProductType.PasswordManager,
        };

        var newPlans = new List<Plan>
        {
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BitwardenProduct = BitwardenProductType.PasswordManager,

            },
            new()
            {
                Type = PlanType.EnterpriseAnnually,
                BaseSeats = 2,
                BaseServiceAccount = 2,
                BitwardenProduct = BitwardenProductType.SecretsManager,

            }
        };

        var organizationUpgradeQuery = sutProvider.GetDependency<IOrganizationUpgradeQuery>();
        organizationUpgradeQuery.GetOrgById(organizationId).Returns(organization);
        organizationUpgradeQuery.ExistingPlan(PlanType.TeamsAnnually).Returns(existingPlan);
        organizationUpgradeQuery.NewPlans(PlanType.EnterpriseAnnually).Returns(newPlans);

        var validateUpgradeCommand = sutProvider.GetDependency<IValidateUpgradeCommand>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var referenceEventService = sutProvider.GetDependency<IReferenceEventService>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        var paymentService = sutProvider.GetDependency<IPaymentService>();
        var organizationSignUpValidationStrategy = sutProvider.GetDependency<IOrganizationSignUpValidationStrategy>();

        var command = new OrganizationUpgradePlanCommand(
            organizationUpgradeQuery,
            validateUpgradeCommand,
            organizationService,
            referenceEventService,
            currentContext,
            paymentService,
            organizationSignUpValidationStrategy
        );

        await Assert.ThrowsAsync<BadRequestException>(() => command.UpgradePlanAsync(organizationId, upgrade));
    }
}
