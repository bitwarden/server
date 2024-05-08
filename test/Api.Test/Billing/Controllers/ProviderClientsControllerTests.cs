using System.Security.Claims;
using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(ProviderClientsController))]
[SutProviderCustomize]
public class ProviderClientsControllerTests
{
    #region CreateAsync
    [Theory, BitAutoData]
    public async Task CreateAsync_FFDisabled_NotFound(
        Guid providerId,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(false);

        var result = await sutProvider.Sut.CreateAsync(providerId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NoPrincipalUser_Unauthorized(
        Guid providerId,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        var result = await sutProvider.Sut.CreateAsync(providerId, requestBody);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NotProviderAdmin_Unauthorized(
        Guid providerId,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(new User());

        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(false);

        var result = await sutProvider.Sut.CreateAsync(providerId, requestBody);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NoProvider_NotFound(
        Guid providerId,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(new User());

        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .ReturnsNull();

        var result = await sutProvider.Sut.CreateAsync(providerId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_MissingClientOrganization_ServerError(
        Guid providerId,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        var user = new User();

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);

        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(new Provider());

        var clientOrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IProviderService>().CreateOrganizationAsync(
                providerId,
                Arg.Any<OrganizationSignup>(),
                requestBody.OwnerEmail,
                user)
            .Returns(new ProviderOrganization
            {
                OrganizationId = clientOrganizationId
            });

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(clientOrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.CreateAsync(providerId, requestBody);

        Assert.IsType<ProblemHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_OK(
        Guid providerId,
        CreateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        var user = new User();

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(true);

        var provider = new Provider();

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        var clientOrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IProviderService>().CreateOrganizationAsync(
                providerId,
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

        var result = await sutProvider.Sut.CreateAsync(providerId, requestBody);

        Assert.IsType<Ok>(result);

        await sutProvider.GetDependency<IProviderBillingService>().Received(1).CreateCustomerForClientOrganization(
            provider,
            clientOrganization);
    }
    #endregion

    #region UpdateAsync
    [Theory, BitAutoData]
    public async Task UpdateAsync_FFDisabled_NotFound(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(false);

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NotProviderAdmin_Unauthorized(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(false);

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NoProvider_NotFound(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .ReturnsNull();

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NoProviderOrganization_NotFound(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        Provider provider,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .ReturnsNull();

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NoOrganization_ServerError(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        Provider provider,
        ProviderOrganization providerOrganization,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(providerOrganization.OrganizationId)
            .ReturnsNull();

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        Assert.IsType<ProblemHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NoContent(
        Guid providerId,
        Guid providerOrganizationId,
        UpdateClientOrganizationRequestBody requestBody,
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<ProviderClientsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(providerId)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId)
            .Returns(provider);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganizationId)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(providerOrganization.OrganizationId)
            .Returns(organization);

        var result = await sutProvider.Sut.UpdateAsync(providerId, providerOrganizationId, requestBody);

        await sutProvider.GetDependency<IProviderBillingService>().Received(1)
            .AssignSeatsToClientOrganization(
                provider,
                organization,
                requestBody.AssignedSeats);

        Assert.IsType<Ok>(result);
    }
    #endregion
}
