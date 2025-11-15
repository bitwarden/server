using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUp;

[SutProviderCustomize]
public class ProviderClientOrganizationSignUpCommandTests
{
    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public async Task SignupClientAsync_ValidParameters_CreatesOrganizationSuccessfully(
        PlanType planType,
        OrganizationSignup signup,
        string collectionName,
        SutProvider<ProviderClientOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.AdditionalSeats = 15;
        signup.CollectionName = collectionName;

        var plan = MockPlans.Get(signup.Plan);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns(plan);

        var result = await sutProvider.Sut.SignUpClientOrganizationAsync(signup);

        Assert.NotNull(result.Organization);
        Assert.NotNull(result.DefaultCollection);
        Assert.Equal(collectionName, result.DefaultCollection.Name);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<Organization>(o =>
                    o.Name == signup.Name &&
                    o.BillingEmail == signup.BillingEmail &&
                    o.PlanType == plan.Type &&
                    o.Seats == signup.AdditionalSeats &&
                    o.MaxCollections == plan.PasswordManager.MaxCollections &&
                    o.UsePasswordManager == true &&
                    o.UseSecretsManager == false &&
                    o.Status == OrganizationStatusType.Created
                )
            );

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<Collection>(c =>
                    c.Name == collectionName &&
                    c.OrganizationId == result.Organization.Id
                ),
                Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>()
            );

        await sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<OrganizationApiKey>(k =>
                    k.OrganizationId == result.Organization.Id &&
                    k.Type == OrganizationApiKeyType.Default
                )
            );

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(o => o.Id == result.Organization.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task SignupClientAsync_NullPlan_ThrowsBadRequestException(
        OrganizationSignup signup,
        SutProvider<ProviderClientOrganizationSignUpCommand> sutProvider)
    {
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns((Plan)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpClientOrganizationAsync(signup));

        Assert.Contains(ProviderClientOrganizationSignUpCommand.PlanNullErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignupClientAsync_NegativeAdditionalSeats_ThrowsBadRequestException(
        OrganizationSignup signup,
        SutProvider<ProviderClientOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = PlanType.TeamsMonthly;
        signup.AdditionalSeats = -5;

        var plan = MockPlans.Get(signup.Plan);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns(plan);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpClientOrganizationAsync(signup));

        Assert.Contains(ProviderClientOrganizationSignUpCommand.AdditionalSeatsNegativeErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task SignupClientAsync_WhenExceptionIsThrown_CleanupIsPerformed(
        PlanType planType,
        OrganizationSignup signup,
        SutProvider<ProviderClientOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;

        var plan = MockPlans.Get(signup.Plan);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns(plan);

        sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .When(x => x.CreateAsync(Arg.Any<OrganizationApiKey>()))
            .Do(_ => throw new Exception());

        var thrownException = await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignUpClientOrganizationAsync(signup));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<Organization>(o => o.Name == signup.Name));

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(Arg.Any<Guid>());
    }
}
