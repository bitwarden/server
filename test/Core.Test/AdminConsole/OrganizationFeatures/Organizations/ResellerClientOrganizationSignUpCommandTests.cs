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
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class ResellerClientOrganizationSignUpCommandTests
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
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;
        signup.AdditionalSeats = 15;
        signup.CollectionName = collectionName;

        var plan = StaticStore.GetPlan(signup.Plan);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns(plan);

        var result = await sutProvider.Sut.SignupClientAsync(signup);

        Assert.NotNull(result.organization);
        Assert.NotNull(result.defaultCollection);
        Assert.Equal(collectionName, result.defaultCollection.Name);

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

        await sutProvider.GetDependency<IReferenceEventService>()
            .Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.Seats == result.organization.Seats &&
                referenceEvent.Storage == result.organization.MaxStorageGb &&
                referenceEvent.SignupInitiationPath == signup.InitiationPath
            ));

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<Collection>(c =>
                    c.Name == collectionName &&
                    c.OrganizationId == result.organization.Id
                ),
                Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>()
            );

        await sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<OrganizationApiKey>(k =>
                    k.OrganizationId == result.organization.Id &&
                    k.Type == OrganizationApiKeyType.Default
                )
            );

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(o => o.Id == result.organization.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task SignupClientAsync_NullPlan_ThrowsBadRequestException(
        OrganizationSignup signup,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns((Plan)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignupClientAsync(signup));

        Assert.Contains("Password Manager Plan was null", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignupClientAsync_NegativeAdditionalSeats_ThrowsBadRequestException(
        OrganizationSignup signup,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = PlanType.TeamsMonthly;
        signup.AdditionalSeats = -5;

        var plan = StaticStore.GetPlan(signup.Plan);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns(plan);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignupClientAsync(signup));

        Assert.Contains("You can't subtract Password Manager seats", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task SignupClientAsync_WhenExceptionIsThrown_CleanupIsPerformed(
        PlanType planType,
        OrganizationSignup signup,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        signup.Plan = planType;

        var plan = StaticStore.GetPlan(signup.Plan);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(signup.Plan)
            .Returns(plan);

        sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .When(x => x.CreateAsync(Arg.Any<OrganizationApiKey>()))
            .Do(_ => throw new Exception());

        var thrownException = await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignupClientAsync(signup));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<Organization>(o => o.Name == signup.Name));

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(Arg.Any<Guid>());
    }
}
