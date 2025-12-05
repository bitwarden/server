using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUp;

[SutProviderCustomize]
public class CloudICloudOrganizationSignUpCommandTests
{
    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2025)]
    public async Task SignUp_PM_Family_Passes(PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;

        var plan = MockPlans.Get(signup.Plan);

        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromSecretsManagerTrial = false;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == null
                && o.SmServiceAccounts == null));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        Assert.NotNull(result.Organization);
        Assert.NotNull(result.OrganizationUser);

        await sutProvider.GetDependency<IOrganizationBillingService>().Received(1).Finalize(
            Arg.Is<OrganizationSale>(sale =>
                sale.CustomerSetup.TokenizedPaymentSource.Type == signup.PaymentMethodType.Value &&
                sale.CustomerSetup.TokenizedPaymentSource.Token == signup.PaymentToken &&
                sale.CustomerSetup.TaxInformation.Country == signup.TaxInfo.BillingAddressCountry &&
                sale.CustomerSetup.TaxInformation.PostalCode == signup.TaxInfo.BillingAddressPostalCode &&
                sale.SubscriptionSetup.PlanType == plan.Type &&
                sale.SubscriptionSetup.PasswordManagerOptions.Seats == signup.AdditionalSeats &&
                sale.SubscriptionSetup.PasswordManagerOptions.Storage == signup.AdditionalStorageGb &&
                sale.SubscriptionSetup.SecretsManagerOptions == null));
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2025)]
    public async Task SignUp_AssignsOwnerToDefaultCollection
        (PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        // Extract orgUserId when created
        Guid? orgUserId = null;
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .CreateAsync(Arg.Do<OrganizationUser>(ou => orgUserId = ou.Id));

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        // Assert: created a Can Manage association for the default collection
        Assert.NotNull(orgUserId);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).CreateAsync(
            Arg.Any<Collection>(),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(cas => cas == null),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(cas =>
                cas.Count() == 1 &&
                cas.All(c =>
                    c.Id == orgUserId &&
                    !c.ReadOnly &&
                    !c.HidePasswords &&
                    c.Manage)));

        Assert.NotNull(result.Organization);
        Assert.NotNull(result.OrganizationUser);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    public async Task SignUp_SM_Passes(PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;

        var plan = MockPlans.Get(signup.Plan);

        signup.UseSecretsManager = true;
        signup.AdditionalSeats = 15;
        signup.AdditionalSmSeats = 10;
        signup.AdditionalServiceAccounts = 20;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.IsFromSecretsManagerTrial = false;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == plan.SecretsManager.BaseSeats + signup.AdditionalSmSeats
                && o.SmServiceAccounts == plan.SecretsManager.BaseServiceAccount + signup.AdditionalServiceAccounts));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        Assert.NotNull(result.Organization);
        Assert.NotNull(result.OrganizationUser);

        await sutProvider.GetDependency<IOrganizationBillingService>().Received(1).Finalize(
            Arg.Is<OrganizationSale>(sale =>
                sale.CustomerSetup.TokenizedPaymentSource.Type == signup.PaymentMethodType.Value &&
                sale.CustomerSetup.TokenizedPaymentSource.Token == signup.PaymentToken &&
                sale.CustomerSetup.TaxInformation.Country == signup.TaxInfo.BillingAddressCountry &&
                sale.CustomerSetup.TaxInformation.PostalCode == signup.TaxInfo.BillingAddressPostalCode &&
                sale.SubscriptionSetup.PlanType == plan.Type &&
                sale.SubscriptionSetup.PasswordManagerOptions.Seats == signup.AdditionalSeats &&
                sale.SubscriptionSetup.PasswordManagerOptions.Storage == signup.AdditionalStorageGb &&
                sale.SubscriptionSetup.SecretsManagerOptions.Seats == signup.AdditionalSmSeats &&
                sale.SubscriptionSetup.SecretsManagerOptions.ServiceAccounts == signup.AdditionalServiceAccounts));
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SignUp_SM_Throws_WhenManagedByMSP(PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.UseSecretsManager = true;
        signup.AdditionalSeats = 15;
        signup.AdditionalSmSeats = 10;
        signup.AdditionalServiceAccounts = 20;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.IsFromProvider = true;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains("Organizations with a Managed Service Provider do not support Secrets Manager.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_SecretManager_AdditionalServiceAccounts_NotAllowedByPlan_ShouldThrowException(OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.AdditionalSmSeats = 0;
        signup.AdditionalSeats = 0;
        signup.Plan = PlanType.Free;
        signup.UseSecretsManager = true;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.AdditionalServiceAccounts = 10;
        signup.AdditionalStorageGb = 0;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains("Plan does not allow additional Machine Accounts.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_SMSeatsGreatThanPMSeat_ShouldThrowException(OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.AdditionalSmSeats = 100;
        signup.AdditionalSeats = 10;
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.UseSecretsManager = true;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.AdditionalServiceAccounts = 10;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
           () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains("You cannot have more Secrets Manager seats than Password Manager seats", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_InvalidateServiceAccount_ShouldThrowException(OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.AdditionalSmSeats = 10;
        signup.AdditionalSeats = 10;
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.UseSecretsManager = true;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.AdditionalServiceAccounts = -10;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains("You can't subtract Machine Accounts!", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_Free_ExistingFreeOrgAdmin_ThrowsBadRequest(
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var signup = new OrganizationSignup
        {
            Plan = PlanType.Free,
            IsFromProvider = false,
            Owner = new User { Id = Guid.NewGuid() }
        };

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id)
            .Returns(1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains("You can only be an admin of one free organization.", exception.Message);
    }
}
