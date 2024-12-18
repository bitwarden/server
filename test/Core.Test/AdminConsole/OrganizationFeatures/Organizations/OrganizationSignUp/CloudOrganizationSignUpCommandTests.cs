using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
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

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUp;

[SutProviderCustomize]
public class CloudICloudOrganizationSignUpCommandTests
{
    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUp_PM_Family_Passes(PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;

        var plan = StaticStore.GetPlan(signup.Plan);

        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromSecretsManagerTrial = false;

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == null
                && o.SmServiceAccounts == null));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.Seats == result.Organization.Seats &&
                referenceEvent.Storage == result.Organization.MaxStorageGb));
        // TODO: add reference events for SmSeats and Service Accounts - see AC-1481

        Assert.NotNull(result.Organization);
        Assert.NotNull(result.OrganizationUser);

        await sutProvider.GetDependency<IPaymentService>().Received(1).PurchaseOrganizationAsync(
            Arg.Any<Organization>(),
            signup.PaymentMethodType.Value,
            signup.PaymentToken,
            plan,
            signup.AdditionalStorageGb,
            signup.AdditionalSeats,
            signup.PremiumAccessAddon,
            signup.TaxInfo,
            false,
            signup.AdditionalSmSeats.GetValueOrDefault(),
            signup.AdditionalServiceAccounts.GetValueOrDefault(),
            signup.UseSecretsManager
        );
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUp_AssignsOwnerToDefaultCollection
        (PlanType planType, OrganizationSignup signup, SutProvider<CloudOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;

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

        var plan = StaticStore.GetPlan(signup.Plan);

        signup.UseSecretsManager = true;
        signup.AdditionalSeats = 15;
        signup.AdditionalSmSeats = 10;
        signup.AdditionalServiceAccounts = 20;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.IsFromSecretsManagerTrial = false;

        var result = await sutProvider.Sut.SignUpOrganizationAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == plan.SecretsManager.BaseSeats + signup.AdditionalSmSeats
                && o.SmServiceAccounts == plan.SecretsManager.BaseServiceAccount + signup.AdditionalServiceAccounts));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.Seats == result.Organization.Seats &&
                referenceEvent.Storage == result.Organization.MaxStorageGb));
        // TODO: add reference events for SmSeats and Service Accounts - see AC-1481

        Assert.NotNull(result.Organization);
        Assert.NotNull(result.OrganizationUser);

        await sutProvider.GetDependency<IPaymentService>().Received(1).PurchaseOrganizationAsync(
            Arg.Any<Organization>(),
            signup.PaymentMethodType.Value,
            signup.PaymentToken,
            Arg.Is<Plan>(plan),
            signup.AdditionalStorageGb,
            signup.AdditionalSeats,
            signup.PremiumAccessAddon,
            signup.TaxInfo,
            false,
            signup.AdditionalSmSeats.GetValueOrDefault(),
            signup.AdditionalServiceAccounts.GetValueOrDefault(),
            signup.IsFromSecretsManagerTrial
        );
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

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpOrganizationAsync(signup));
        Assert.Contains("You can't subtract Machine Accounts!", exception.Message);
    }
}
