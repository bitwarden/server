using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class SetOrganizationKeyIdCommandTests
{
    private const string _otherKeyId = "fedcba9876543210fedcba9876543210";

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_OrganizationNotFound_ThrowsAndWritesNothing(
        Guid organizationId,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organizationId).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetOrganizationKeyIdAsync(organizationId, organizationKeyId));

        await organizationRepository.DidNotReceiveWithAnyArgs().SetOrganizationKeyIdAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_OrganizationPending_ThrowsAndWritesNothing(
        Organization organization,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        organization.Status = OrganizationStatusType.Pending;
        organization.OrganizationKeyId = null;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId));

        Assert.Equal("Organization is not initialized.", exception.Message);
        await organizationRepository.DidNotReceiveWithAnyArgs().SetOrganizationKeyIdAsync(default, default!);
    }

    [Theory]
    [BitAutoData(OrganizationStatusType.Created)]
    [BitAutoData(OrganizationStatusType.Managed)]
    public async Task SetOrganizationKeyIdAsync_NoKeyIdRecorded_StoresTheKeyId(
        OrganizationStatusType status,
        Organization organization,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        organization.Status = status;
        organization.OrganizationKeyId = null;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationRepository.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId).Returns(true);

        await sutProvider.Sut.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId);

        await organizationRepository.Received(1).SetOrganizationKeyIdAsync(organization.Id, organizationKeyId);
    }

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_SameKeyIdAlreadyRecorded_Succeeds(
        Organization organization,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        organization.Status = OrganizationStatusType.Created;
        organization.OrganizationKeyId = organizationKeyId.ToString();

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationRepository.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId).Returns(false);

        await sutProvider.Sut.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId);

        await organizationRepository.Received(1).SetOrganizationKeyIdAsync(organization.Id, organizationKeyId);
    }

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_DifferentKeyIdAlreadyRecorded_Throws(
        Organization organization,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        organization.Status = OrganizationStatusType.Created;
        organization.OrganizationKeyId = _otherKeyId;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationRepository.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId));

        Assert.Equal("Organization key id is already set.", exception.Message);
    }
}
