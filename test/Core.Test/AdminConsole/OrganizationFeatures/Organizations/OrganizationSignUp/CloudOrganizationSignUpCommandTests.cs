using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUp;

[SutProviderCustomize]
public class CloudOrganizationSignUpCommandTests
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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == null
                && o.SmServiceAccounts == null
                && o.UseRiskInsights == plan.HasRiskInsights));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        // Families does not enable RiskInsights, so the created organization must have UseRiskInsights false.
        Assert.False(plan.HasRiskInsights);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o => !o.UseRiskInsights));

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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == plan.SecretsManager.BaseSeats + signup.AdditionalSmSeats
                && o.SmServiceAccounts == plan.SecretsManager.BaseServiceAccount + signup.AdditionalServiceAccounts
                && o.UseRiskInsights == plan.HasRiskInsights));
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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains(new SecretsManagerMspUnsupportedError().Message, exception.Message);
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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains(new PlanDoesNotAllowAdditionalMachineAccountsError().Message, exception.Message);
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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
           () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains(new SecretsManagerSeatsMustNotExceedPasswordManagerSeatsError().Message, exception.Message);
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
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains(new CannotSubtractMachineAccountsError().Message, exception.Message);
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

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains(new FreeOrgAdminLimitError().Message, exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SignUpAsync_WhenSingleOrgPolicyIsEnabled_OwnerBelongsToAnotherOrgAsUser_ThrowsBadRequest(
        PlanType planType, OrganizationSignup signup,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        signup.Plan = planType;
        signup.AdditionalSeats = 15;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        // User has SingleOrg policy from another org
        organizationUser.UserId = signup.Owner.Id;
        organizationUser.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.EnabledForAnotherOrganization());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains(new UserCannotCreateOrg().Message, exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SignUpAsync_WithoutSingleOrgPolicy_Succeeds(
        PlanType planType, OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        signup.Plan = planType;
        signup.AdditionalSeats = 15;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = null;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        // No SingleOrg policy
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.NoSinglePolicyOrganizationsForUser());

        // Act
        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        // Assert
        Assert.NotNull(result.Organization);
        Assert.NotNull(result.OrganizationUser);

        // Enterprise enables RiskInsights, so the created organization must have UseRiskInsights set to true.
        var enterprisePlan = MockPlans.Get(signup.Plan);
        Assert.True(enterprisePlan.HasRiskInsights);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o => o.UseRiskInsights));
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUpOrganizationAsync_WithTrialLengthAboveMaximum_ThrowsBadRequestException(
        PlanType planType, OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.TrialLength = 31;
        signup.AdditionalSeats = 0;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Equal(new TrialLengthOutOfRangeError().Message, ex.Message);
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUpOrganizationAsync_WithNegativeTrialLength_ThrowsBadRequestException(
        PlanType planType, OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.TrialLength = -1;
        signup.AdditionalSeats = 0;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Equal(new TrialLengthOutOfRangeError().Message, ex.Message);
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUpOrganizationAsync_WithTrialLengthAtMinimum_Succeeds(
        PlanType planType, OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.TrialLength = 0;
        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromSecretsManagerTrial = false;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUpOrganizationAsync_WithTrialLengthAtMaximum_Succeeds(
        PlanType planType, OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.TrialLength = 30;
        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromSecretsManagerTrial = false;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUpOrganizationAsync_WithoutTrialLength_Succeeds(
        PlanType planType, OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.TrialLength = null;
        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromSecretsManagerTrial = false;
        signup.IsFromProvider = false;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_PassesSeatsToPurchaseCommand(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.AdditionalSeats = 10;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = 7;
        signup.UsePrivilegedControls = true;
        signup.PamSeats = 6;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionCreation>())
            .Returns(new Subscription());

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().Received(1).Run(
            Arg.Any<Organization>(),
            Arg.Is<OrganizationSubscriptionCreation>(c =>
                c.PlanType == PlanType.EnterpriseAnnually &&
                c.SecretsManager == null &&
                c.PrivilegedControls != null && c.PrivilegedControls.Seats == 6));
        await sutProvider.GetDependency<IOrganizationBillingService>().DidNotReceive().Finalize(Arg.Any<OrganizationSale>());
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SeatsMissing_Throws(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));

        Assert.Equal("Privileged Controls seats are required.", exception.Message);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive().CreateAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().DidNotReceiveWithAnyArgs()
            .Run(default!, default!);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SeatsAbovePmSeats_Throws(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));

        Assert.Equal("You cannot have more Privileged Controls seats than Password Manager seats.", exception.Message);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive().CreateAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().DidNotReceiveWithAnyArgs()
            .Run(default!, default!);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SeatsEqualPmSeats_Succeeds(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 10);

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().Received(1).Run(
            Arg.Any<Organization>(),
            Arg.Is<OrganizationSubscriptionCreation>(c => c.PrivilegedControls != null && c.PrivilegedControls.Seats == 10));
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SignUpAsync_FlagEnabled_WithoutUsePrivilegedControls_UsesFinalize(
        PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.AdditionalSeats = 10;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = null;
        signup.UsePrivilegedControls = false;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationBillingService>().Received(1).Finalize(Arg.Any<OrganizationSale>());
        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().DidNotReceiveWithAnyArgs()
            .Run(default!, default!);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_MapsSignupOntoPurchase(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.AdditionalSeats = 10;
        signup.AdditionalStorageGb = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = 7;
        signup.SkipTrial = false;
        signup.UsePrivilegedControls = true;
        signup.PamSeats = 6;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionCreation>())
            .Returns(new Subscription());

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().Received(1).Run(
            Arg.Any<Organization>(),
            Arg.Is<OrganizationSubscriptionCreation>(c =>
                c.PlanType == PlanType.EnterpriseAnnually &&
                c.PasswordManager.Seats == signup.AdditionalSeats &&
                !c.PasswordManager.PremiumAccess &&
                c.SecretsManager == null &&
                c.PrivilegedControls != null && c.PrivilegedControls.Seats == signup.PamSeats &&
                c.Trial.Length == signup.TrialLength &&
                c.Owner == signup.Owner &&
                c.CustomerSetup != null && c.CustomerSetup.TokenizedPaymentSource != null &&
                c.CustomerSetup.TokenizedPaymentSource.Token == signup.PaymentToken));
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_CommandThrows_DoesNotPersistOrganization(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.AdditionalSeats = 10;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = 7;
        signup.UsePrivilegedControls = true;
        signup.PamSeats = 6;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionCreation>())
            .ThrowsAsync(new BadRequestException("Invalid Privileged Controls plan selected."));

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpOrganizationAsync(signup));

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive().CreateAsync(Arg.Any<Organization>());
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_OrganizationSaveFails_RecoversCharges(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.AdditionalSeats = 10;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = 7;
        signup.UsePrivilegedControls = true;
        signup.PamSeats = 6;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionCreation>())
            .Returns(new Subscription());
        sutProvider.GetDependency<IOrganizationRepository>().CreateAsync(Arg.Any<Organization>())
            .ThrowsAsync(new InvalidOperationException("persistence failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.SignUpOrganizationAsync(signup));

        // A persistence failure after the subscription is created must roll back the charges.
        await sutProvider.GetDependency<IStripePaymentService>().Received(1)
            .CancelAndRecoverChargesAsync(Arg.Any<Organization>());
    }

    [Theory]
    [BitAutoData(TrialInitiationConstants.SalesAssistedTrialInitiationPath)]
    [BitAutoData("Password Manager trial from marketing website")]
    [BitAutoData((string?)null)]
    public async Task SignUpAsync_PrivilegedControls_FlagOn_ProvisionsWithoutToken(
        string initiationPath,
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 6, pamBillingSupport: true);
        signup.InitiationPath = initiationPath;
        signup.SalesAssistedToken = null;

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsProvisionedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SalesAssistedWithValidToken_FlagOff_Provisions(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 6, pamBillingSupport: false);
        signup.InitiationPath = TrialInitiationConstants.SalesAssistedTrialInitiationPath;
        signup.SalesAssistedToken = "sales-assisted-token";
        // The token's email matches the authenticated owner whatever its case.
        ArrangeSalesAssistedToken(sutProvider, signup.SalesAssistedToken,
            SalesAssistedTokenable(signup.Owner.Email.ToUpperInvariant()));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsProvisionedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SalesAssistedWithoutToken_FlagOff_IgnoresPrivilegedControls(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11, pamBillingSupport: false);
        signup.InitiationPath = TrialInitiationConstants.SalesAssistedTrialInitiationPath;
        signup.SalesAssistedToken = null;

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsIgnoredAsync(sutProvider);
        sutProvider.GetDependency<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>()
            .DidNotReceiveWithAnyArgs().TryUnprotect(default!, out _);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SalesAssistedTokenForAnotherEmail_FlagOff_IgnoresPrivilegedControls(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11, pamBillingSupport: false);
        signup.InitiationPath = TrialInitiationConstants.SalesAssistedTrialInitiationPath;
        signup.SalesAssistedToken = "sales-assisted-token";
        ArrangeSalesAssistedToken(sutProvider, signup.SalesAssistedToken,
            SalesAssistedTokenable("someone-else@example.com"));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsIgnoredAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SalesAssistedExpiredToken_FlagOff_IgnoresPrivilegedControls(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11, pamBillingSupport: false);
        signup.InitiationPath = TrialInitiationConstants.SalesAssistedTrialInitiationPath;
        signup.SalesAssistedToken = "sales-assisted-token";
        ArrangeSalesAssistedToken(sutProvider, signup.SalesAssistedToken,
            SalesAssistedTokenable(signup.Owner.Email, expirationDate: DateTime.UtcNow.AddDays(-1)));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsIgnoredAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_SalesAssistedTokenUnreadable_FlagOff_IgnoresPrivilegedControls(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11, pamBillingSupport: false);
        signup.InitiationPath = TrialInitiationConstants.SalesAssistedTrialInitiationPath;
        signup.SalesAssistedToken = "tampered-token";
        sutProvider.GetDependency<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>()
            .TryUnprotect(signup.SalesAssistedToken, out Arg.Any<SalesAssistedRegistrationTokenable>())
            .Returns(false);

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsIgnoredAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_PrivilegedControls_ValidTokenOnSelfServePath_FlagOff_IgnoresPrivilegedControls(
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11, pamBillingSupport: false);
        signup.InitiationPath = "Password Manager trial from marketing website";
        signup.SalesAssistedToken = "sales-assisted-token";
        ArrangeSalesAssistedToken(sutProvider, signup.SalesAssistedToken, SalesAssistedTokenable(signup.Owner.Email));

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsIgnoredAsync(sutProvider);
    }

    [Theory]
    [BitAutoData("Password Manager trial from marketing website")]
    [BitAutoData((string?)null)]
    public async Task SignUpAsync_PrivilegedControls_SelfServeWithFlagOff_IgnoresPrivilegedControls(
        string initiationPath,
        OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        // Seats above the Password Manager seats would fail validation if the request were honored.
        ArrangePrivilegedControlsSignup(signup, sutProvider, pamSeats: 11, pamBillingSupport: false);
        signup.InitiationPath = initiationPath;
        signup.SalesAssistedToken = null;

        await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await AssertPrivilegedControlsIgnoredAsync(sutProvider);
    }

    private static async Task AssertPrivilegedControlsProvisionedAsync(
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().Received(1).Run(
            Arg.Any<Organization>(),
            Arg.Is<OrganizationSubscriptionCreation>(c => c.PrivilegedControls != null && c.PrivilegedControls.Seats == 6));
        await sutProvider.GetDependency<IOrganizationBillingService>().DidNotReceive().Finalize(Arg.Any<OrganizationSale>());
    }

    // Callers ask for 11 seats, above the Password Manager seats, which would fail validation if the request were honored.
    private static async Task AssertPrivilegedControlsIgnoredAsync(
        SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o => o.PamSeats == null && !o.UsePam));
        await sutProvider.GetDependency<IOrganizationBillingService>().Received(1).Finalize(Arg.Any<OrganizationSale>());
        await sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>().DidNotReceiveWithAnyArgs()
            .Run(default!, default!);
    }

    private static SalesAssistedRegistrationTokenable SalesAssistedTokenable(
        string email, DateTime? expirationDate = null) =>
        new(email, name: null) { ExpirationDate = expirationDate ?? DateTime.UtcNow.AddDays(5) };

    private static void ArrangeSalesAssistedToken(
        SutProvider<CloudOrganizationSignUpCommand> sutProvider,
        string token,
        SalesAssistedRegistrationTokenable tokenable) =>
        sutProvider.GetDependency<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>()
            .TryUnprotect(token, out Arg.Any<SalesAssistedRegistrationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = tokenable;
                return true;
            });

    private static void ArrangePrivilegedControlsSignup(
        OrganizationSignup signup,
        SutProvider<CloudOrganizationSignUpCommand> sutProvider,
        int? pamSeats,
        bool pamBillingSupport = true)
    {
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.AdditionalSeats = 10;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromProvider = false;
        signup.TrialLength = 7;
        signup.UsePrivilegedControls = true;
        signup.PamSeats = pamSeats;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(pamBillingSupport);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(signup.Plan).Returns(MockPlans.Get(signup.Plan));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(signup.Owner.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(signup.Owner.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        sutProvider.GetDependency<ICreateOrganizationSubscriptionCommand>()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionCreation>())
            .Returns(new Subscription());
    }
}
