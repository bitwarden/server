using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

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
        ConfigureStableProviderInputs(provider, sutProvider);

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
        ConfigureStableProviderInputs(provider, sutProvider);

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
                    signup.Keys.PublicKey == requestBody.KeyPair.PublicKey &&
                    signup.Keys.WrappedPrivateKey == requestBody.KeyPair.EncryptedPrivateKey &&
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

    #region AddExistingOrganizationAsync

    [Theory, BitAutoData]
    public async Task AddExistingOrganizationAsync_ServiceUser_Unauthorized(
        Provider provider,
        AddExistingOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        ConfigureStableProviderInputs(provider, sutProvider);

        var result = await sutProvider.Sut.AddExistingOrganizationAsync(provider.Id, requestBody);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganizationAsync_NotOrgOwner_Unauthorized(
        Provider provider,
        AddExistingOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        ConfigureStableProviderInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(requestBody.OrganizationId)
            .Returns(false);

        var result = await sutProvider.Sut.AddExistingOrganizationAsync(provider.Id, requestBody);

        AssertUnauthorized(result);
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganizationAsync_OrgNotAddable_NotFound(
        Provider provider,
        AddExistingOrganizationRequestBody requestBody,
        Guid userId,
        SutProvider<ProviderClientsController> sutProvider)
    {
        ConfigureStableProviderInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(requestBody.OrganizationId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetAddableToProviderByUserIdAsync(userId, provider.Type)
            .Returns([]);

        var result = await sutProvider.Sut.AddExistingOrganizationAsync(provider.Id, requestBody);

        AssertNotFound(result);
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganizationAsync_Ok(
        Provider provider,
        AddExistingOrganizationRequestBody requestBody,
        Organization organization,
        Guid userId,
        SutProvider<ProviderClientsController> sutProvider)
    {
        organization.Id = requestBody.OrganizationId;

        ConfigureStableProviderInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(requestBody.OrganizationId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetAddableToProviderByUserIdAsync(userId, provider.Type)
            .Returns([organization]);

        var result = await sutProvider.Sut.AddExistingOrganizationAsync(provider.Id, requestBody);

        await sutProvider.GetDependency<IProviderBillingService>().Received(1)
            .AddExistingOrganization(provider, organization, requestBody.Key);

        Assert.IsType<Ok>(result);
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
        providerOrganization.ProviderId = provider.Id;

        ConfigureStableProviderInputs(provider, sutProvider);

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

        var response = (JsonHttpResult<ErrorResponseModel>)result;
        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("Service users cannot purchase additional seats.", response.Value.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ProviderOrganizationBelongsToDifferentProvider_NotFound(
        Provider provider,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        ProviderOrganization providerOrganization,
        SutProvider<ProviderClientsController> sutProvider)
    {
        ConfigureStableProviderInputs(provider, sutProvider);

        providerOrganization.ProviderId = Guid.NewGuid();

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        var result = await sutProvider.Sut.UpdateAsync(provider.Id, providerOrganizationId, requestBody);

        AssertNotFound(result);
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
        providerOrganization.ProviderId = provider.Id;

        ConfigureStableProviderInputs(provider, sutProvider);

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

    private static void ConfigureStableProviderInputs(
        Provider provider,
        SutProvider<ProviderClientsController> sutProvider)
    {
        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
    }

    private static void AssertUnauthorized(IResult result)
    {
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    private static void AssertNotFound(IResult result)
    {
        Assert.IsType<NotFound<ErrorResponseModel>>(result);
        var response = ((NotFound<ErrorResponseModel>)result).Value;
        Assert.Equal("Resource not found.", response.Message);
    }
}
