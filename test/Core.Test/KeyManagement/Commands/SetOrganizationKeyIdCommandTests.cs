using Bit.Core.AdminConsole.Entities;
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
    public async Task SetOrganizationKeyIdAsync_OrganizationNotFound_Throws(
        Guid organizationId,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.SetOrganizationKeyIdAsync(organizationId, organizationKeyId).Returns(false);
        organizationRepository.GetByIdAsync(organizationId).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SetOrganizationKeyIdAsync(organizationId, organizationKeyId));
    }

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_NoKeyIdRecorded_StoresTheKeyId(
        Guid organizationId,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.SetOrganizationKeyIdAsync(organizationId, organizationKeyId).Returns(true);

        await sutProvider.Sut.SetOrganizationKeyIdAsync(organizationId, organizationKeyId);

        await organizationRepository.Received(1).SetOrganizationKeyIdAsync(organizationId, organizationKeyId);
        await organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_SameKeyIdAlreadyRecorded_Succeeds(
        Organization organization,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        organization.OrganizationKeyId = organizationKeyId.ToString();

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId).Returns(false);
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        await sutProvider.Sut.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId);

        await organizationRepository.Received(1).SetOrganizationKeyIdAsync(organization.Id, organizationKeyId);
        await organizationRepository.Received(1).GetByIdAsync(organization.Id);
    }

    [Theory, BitAutoData]
    public async Task SetOrganizationKeyIdAsync_DifferentKeyIdAlreadyRecorded_Throws(
        Organization organization,
        KeyId organizationKeyId,
        SutProvider<SetOrganizationKeyIdCommand> sutProvider)
    {
        organization.OrganizationKeyId = _otherKeyId;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId).Returns(false);
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetOrganizationKeyIdAsync(organization.Id, organizationKeyId));

        Assert.Equal("Organization key id is already set.", exception.Message);
    }
}
