using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using OrganizationNotFound = Bit.Core.AdminConsole.Utilities.v2.Shared.OrganizationNotFound;
using OrganizationUserNotFound = Bit.Core.AdminConsole.Utilities.v2.Shared.OrganizationUserNotFound;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.Validation;

[SutProviderCustomize]
public class OrganizationAndOrganizationUserValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationIdIsEmpty_ReturnsOrganizationNotFoundError(
        SutProvider<OrganizationAndOrganizationUserValidator> sutProvider,
        Guid organizationUserId)
    {
        var result = await sutProvider.Sut.ValidateAsync(new OrganizationScope(Guid.Empty), organizationUserId);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFound>(result.AsError);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationUserIdIsEmpty_ReturnsOrganizationUserNotFoundError(
        SutProvider<OrganizationAndOrganizationUserValidator> sutProvider,
        Guid organizationId)
    {
        var result = await sutProvider.Sut.ValidateAsync(new OrganizationScope(organizationId), Guid.Empty);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotFound>(result.AsError);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationDoesNotExist_ReturnsOrganizationNotFoundError(
        SutProvider<OrganizationAndOrganizationUserValidator> sutProvider,
        Guid organizationId,
        Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization?)null);

        var result = await sutProvider.Sut.ValidateAsync(new OrganizationScope(organizationId), organizationUserId);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationNotFound>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationUserDoesNotExist_ReturnsOrganizationUserNotFoundError(
        SutProvider<OrganizationAndOrganizationUserValidator> sutProvider,
        Organization organization,
        Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns((OrganizationUser?)null);

        var result = await sutProvider.Sut.ValidateAsync(new OrganizationScope(organization.Id), organizationUserId);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationUserBelongsToDifferentOrganization_ReturnsOrganizationUserNotFoundError(
        SutProvider<OrganizationAndOrganizationUserValidator> sutProvider,
        Organization organization,
        OrganizationUser organizationUser)
    {
        organizationUser.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var result = await sutProvider.Sut.ValidateAsync(new OrganizationScope(organization.Id), organizationUser.Id);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserNotFound>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMatchingOrganizationAndOrganizationUser_ReturnsValidResult(
        SutProvider<OrganizationAndOrganizationUserValidator> sutProvider,
        Organization organization,
        OrganizationUser organizationUser)
    {
        organizationUser.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        var result = await sutProvider.Sut.ValidateAsync(new OrganizationScope(organization.Id), organizationUser.Id);

        Assert.True(result.IsValid);
        Assert.Same(organization, result.Request.Organization);
        Assert.Same(organizationUser, result.Request.OrganizationUser);
    }
}
