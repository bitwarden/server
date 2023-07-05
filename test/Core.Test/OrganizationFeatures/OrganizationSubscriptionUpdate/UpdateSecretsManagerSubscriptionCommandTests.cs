using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class UpdateSecretsManagerSubscriptionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NoOrganization_Throws(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = null,
            SeatAdjustment = 0
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Organization is not found", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NoSecretsManagerAccess_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            UseSecretsManager = false,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            SeatAdjustment = 1,
            MaxAutoscaleSeats = 1
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
             () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Organization has no access to Secrets Manager.", exception.Message);

        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_SeatsAdustmentGreaterThanMaxAutoscaleSeats_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10,
            PlanType = PlanType.EnterpriseAnnually,
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 10,
            SeatAdjustment = 15
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ServiceAccountsGreaterThanMaxAutoscaleSeats_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 10,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "9"
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 10,
            ServiceAccountsAdjustment = 11
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Cannot set max Service Accounts autoscaling below Service Accounts count", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NullGatewayCustomerId_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            SmServiceAccounts = 5,
            UseSecretsManager = true,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("No payment method found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NullGatewaySubscriptionId_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1"
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("No subscription found.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_OrgWithNullSmSeatOnSeatsAdjustment_ThrowsException(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = null,
            UseSecretsManager = true,
            SmServiceAccounts = 5,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 15,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1"
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 15,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));

        Assert.Contains("Organization has no Secrets Manager seat limit, no need to adjust seats", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.Custom)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    public async Task UpdateSecretsManagerSubscription_WithNonSecretsManagerPlanType_ThrowsBadRequestException(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {

        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            UseSecretsManager = true,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 300,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = false,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Existing plan not found", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateSecretsManagerSubscription_WithHasAdditionalSeatsOptionfalse_ThrowsBadRequestException(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {

        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 300,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = false,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Plan does not allow additional Secrets Manager seats.", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateSecretsManagerSubscription_WithHasAdditionalServiceAccountOptionFalse_ThrowsBadRequestException(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {

        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 300,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organization.Id,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 1
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = false,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Plan does not allow additional Service Accounts", exception.Message, StringComparison.InvariantCultureIgnoreCase);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateSecretsManagerSubscription_ValidInput_Passes(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var previousSeats = organization.SmSeats;
        var previousServiceAccounts = organization.SmServiceAccounts;

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = true,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);
        var additionalSeats = (organization.SmSeats + organizationUpdate.SeatAdjustment) - plans[0].BaseSeats;
        var additionalServiceAccounts =
            (organization.SmServiceAccounts + organizationUpdate.ServiceAccountsAdjustment) - plans[0].BaseServiceAccount;
        var newSeatTotal = organization.SmSeats.GetValueOrDefault() + organizationUpdate.SeatAdjustment;

        var newServiceAccountsTotal = organization.SmServiceAccounts.GetValueOrDefault() + organizationUpdate.ServiceAccountsAdjustment;

        await sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate);
        if (organizationUpdate.ServiceAccountsAdjustment != 0)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .AdjustSeatsAsync(organization, plans[0], additionalSeats.GetValueOrDefault());

            await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(
                Arg.Is<ReferenceEvent>(referenceEvent =>
                    referenceEvent.Type == ReferenceEventType.AdjustSmSeats &&
                    referenceEvent.Id == organization.Id &&
                    referenceEvent.PlanName == plans[0].Name &&
                    referenceEvent.PlanType == plans[0].Type &&
                    referenceEvent.Seats == newSeatTotal &&
                    referenceEvent.PreviousSeats == previousSeats));
        }

        if (organizationUpdate.SeatAdjustment != 0)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .AdjustServiceAccountsAsync(organization, plans[0], additionalServiceAccounts.GetValueOrDefault());

            await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(
                Arg.Is<ReferenceEvent>(referenceEvent =>
                    referenceEvent.Type == ReferenceEventType.AdjustServiceAccounts &&
                    referenceEvent.Id == organization.Id &&
                    referenceEvent.PlanName == plans[0].Name &&
                    referenceEvent.PlanType == plans[0].Type &&
                    referenceEvent.ServiceAccounts == newServiceAccountsTotal &&
                    referenceEvent.PreviousServiceAccounts == previousServiceAccounts));
        }

        if (organizationUpdate.SeatAdjustment != 0)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.SmSeats == newSeatTotal));
        }

        if (organizationUpdate.ServiceAccountsAdjustment != 0)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.SmServiceAccounts == newServiceAccountsTotal));
        }

        if (organizationUpdate.MaxAutoscaleSeats != organization.MaxAutoscaleSmSeats)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
               Arg.Is<Organization>(org =>
                   org.MaxAutoscaleSmSeats == organizationUpdate.MaxAutoscaleServiceAccounts));
        }

        if (organizationUpdate.MaxAutoscaleServiceAccounts != organization.MaxAutoscaleSmServiceAccounts)
        {
            await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.MaxAutoscaleSmServiceAccounts == organizationUpdate.MaxAutoscaleServiceAccounts));
        }

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationMaxSecretsManagerSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, Arg.Any<IEnumerable<string>>());
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationMaxSecretsManagerServiceAccountLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value, Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ThrowsBadRequestException_WhenMaxAutoscaleSeatsBelowSeatCount(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 5,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 4,
            MaxAutoscaleSmServiceAccounts = 300,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                Type = PlanType.EnterpriseAnnually
            }
        };
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 4,
            SeatAdjustment = 1,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5
        };

        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Cannot set max seat autoscaling below seat count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ThrowsBadRequestException_WhenOccupiedSeatsExceedNewSeatTotal(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
            PlanType = PlanType.EnterpriseAnnually
        };
        var plans = new List<Plan>
        {
            new()
            {
                BaseSeats = 5,
                HasAdditionalSeatsOption = true,
                Type = PlanType.EnterpriseAnnually
            }
        };
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 7,
            SeatAdjustment = -3,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 5
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetOccupiedSmSeatCountByOrganizationIdAsync(organizationId).Returns(8);


        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Your organization currently has 8 Secrets Manager seats. Your plan only allows (7) Secrets Manager seats. Remove some Secrets Manager users", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_ThrowsBadRequestException_WhenAdditionalSeatsExceedMaxAdditionalSeats(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
            PlanType = PlanType.EnterpriseAnnually
        };
        var plans = new List<Plan>
        {
            new()
            {
                BaseSeats = 5,
                MaxAdditionalSeats = 2,
                HasAdditionalSeatsOption = true,
                Type = PlanType.EnterpriseAnnually
            }
        };
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 21,
            SeatAdjustment = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Organization plan allows a maximum of 2 additional Secrets Manager seats.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountsAsync_ThrowsBadRequestException_WhenSmServiceAccountsIsNull(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 10,
            UseSecretsManager = true,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
            SmServiceAccounts = null,
            PlanType = PlanType.EnterpriseAnnually
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                Type = PlanType.EnterpriseAnnually
            }
        };
        var update = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 21,
            SeatAdjustment = 10,
            MaxAutoscaleServiceAccounts = 250,
            ServiceAccountsAdjustment = 1
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(update));
        Assert.Contains("Organization has no Service Accounts limit, no need to adjust Service Accounts", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task AutoscaleSeatsAsync_ThrowsBadRequestException_WhenMaxAutoscaleSeatsExceedPlanMaxUsers(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            SmSeats = 3,
            UseSecretsManager = true,
            SmServiceAccounts = 100,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2",
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = true,
                Type = PlanType.EnterpriseAnnually,
                MaxUsers = 10,
                HasAdditionalServiceAccountOption = true
            }
        };
        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 200,
            ServiceAccountsAdjustment = 100
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan has a Secrets Manager seat limit of 10, but you have specified a max autoscale count of 15.Reduce your max autoscale count.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task AutoscaleSeatsAsync_ThrowsBadRequestException_WhenPlanDoesNotAllowSeatAutoscale(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var previousSeats = organization.SmSeats;
        var previousServiceAccounts = organization.SmServiceAccounts;

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =false,
                AllowServiceAccountsAutoscale = true,
                HasAdditionalServiceAccountOption = true,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan does not allow Secrets Manager seat autoscaling", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);

    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task UpdateServiceAccountAutoscaling_ThrowsBadRequestException_WhenPlanDoesNotAllowServiceAccountAutoscale(
        PlanType planType,
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        var organization = new Organization
        {
            Id = organizationId,
            UseSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 200,
            MaxAutoscaleSmSeats = 20,
            MaxAutoscaleSmServiceAccounts = 350,
            PlanType = planType,
            GatewayCustomerId = "1",
            GatewaySubscriptionId = "2"
        };

        var previousSeats = organization.SmSeats;
        var previousServiceAccounts = organization.SmServiceAccounts;

        var organizationUpdate = new SecretsManagerSubscriptionUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = 15,
            SeatAdjustment = 5,
            MaxAutoscaleServiceAccounts = 300,
            ServiceAccountsAdjustment = 100
        };
        var plans = new List<Plan>
        {
            new()
            {
                HasAdditionalSeatsOption = true,
                AllowSeatAutoscale =true,
                AllowServiceAccountsAutoscale = false,
                HasAdditionalServiceAccountOption = true,
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                BaseSeats = 0,
                BaseServiceAccount = 200
            }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IStaticStoreWrapper>().SecretsManagerPlans.Returns(plans);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
        Assert.Contains("Your plan does not allow Service Accounts autoscaling.", exception.Message);
        await VerifyDependencyNotCalledAsync(sutProvider);

    }

    private static async Task VerifyDependencyNotCalledAsync(SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        await sutProvider.GetDependency<IPaymentService>().DidNotReceive()
            .AdjustServiceAccountsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());
        await sutProvider.GetDependency<IReferenceEventService>().DidNotReceive()
            .RaiseEventAsync(Arg.Any<ReferenceEvent>());
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationMaxSeatLimitReachedEmailAsync(Arg.Any<Organization>(), Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());
    }
}
