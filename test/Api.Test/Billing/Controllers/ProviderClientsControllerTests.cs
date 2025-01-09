using System.Security.Claims;
using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

using static Bit.Api.Test.Billing.Utilities;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(ProviderClientsController))]
[SutProviderCustomize]
public class ProviderClientsControllerTests
{
    #region CreateAsync

    [Theory, BitAutoData]
    public async Task CreateAsync_NoPrincipalUser_Unauthorized(
        Provider provider,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        var result = await sutProvider.Sut.CreateAsync(provider.Id, requestBody);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OK(
        Provider provider,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        ConfigureStableProviderAdminInputs(provider, sutProvider);

        var user = new User();

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        var clientOrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IProviderService>().CreateOrganizationAsync(
                provider.Id,
                Arg.Is<OrganizationSignup>(signup =>
                    signup.Name == requestBody.Name &&
                    signup.Plan == requestBody.PlanType &&
                    signup.AdditionalSeats == requestBody.Seats &&
                    signup.OwnerKey == requestBody.Key &&
                    signup.PublicKey == requestBody.KeyPair.PublicKey &&
                    signup.PrivateKey == requestBody.KeyPair.EncryptedPrivateKey &&
                    signup.CollectionName == requestBody.CollectionName),
                requestBody.OwnerEmail,
                user)
            .Returns(new ProviderOrganization
            {
                OrganizationId = clientOrganizationId
            });

        var clientOrganization = new Organization { Id = clientOrganizationId };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(clientOrganizationId)
            .Returns(clientOrganization);

        var result = await sutProvider.Sut.CreateAsync(provider.Id, requestBody);

        Assert.IsType<Ok>(result);

        await sutProvider.GetDependency<IProviderBillingService>().Received(1).CreateCustomerForClientOrganization(
            provider,
            clientOrganization);
    }

    #endregion

    #region UpdateAsync

    [Theory, BitAutoData]
    public async Task UpdateAsync_ServiceUserMakingPurchase_Unauthorized(
        Provider provider,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<ProviderClientsController> sutProvider)
    {
        organization.PlanType = PlanType.TeamsMonthly;
        organization.Seats = 10;
        organization.Status = OrganizationStatusType.Managed;
        requestBody.AssignedSeats = 20;

        ConfigureStableProviderServiceUserInputs(provider, sutProvider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(providerOrganization.OrganizationId)
            .Returns(organization);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(false);

        sutProvider.GetDependency<IProviderBillingService>().SeatAdjustmentResultsInPurchase(
            provider,
            PlanType.TeamsMonthly,
            10).Returns(true);

        var result = await sutProvider.Sut.UpdateAsync(provider.Id, providerOrganizationId, requestBody);

        AssertUnauthorized(result, message: "Service users cannot purchase additional seats.");
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_Ok(
        Provider provider,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<ProviderClientsController> sutProvider)
    {
        organization.PlanType = PlanType.TeamsMonthly;
        organization.Seats = 10;
        organization.Status = OrganizationStatusType.Managed;
        requestBody.AssignedSeats = 20;

        ConfigureStableProviderServiceUserInputs(provider, sutProvider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(providerOrganization.OrganizationId)
            .Returns(organization);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(false);

        sutProvider.GetDependency<IProviderBillingService>().SeatAdjustmentResultsInPurchase(
            provider,
            PlanType.TeamsMonthly,
            10).Returns(false);

        var result = await sutProvider.Sut.UpdateAsync(provider.Id, providerOrganizationId, requestBody);

        await sutProvider.GetDependency<IProviderBillingService>().Received(1)
            .ScaleSeats(
                provider,
                PlanType.TeamsMonthly,
                10);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1)
            .ReplaceAsync(Arg.Is<Organization>(org => org.Seats == requestBody.AssignedSeats && org.Name == requestBody.Name));

        Assert.IsType<Ok>(result);
    }

    #endregion
}
