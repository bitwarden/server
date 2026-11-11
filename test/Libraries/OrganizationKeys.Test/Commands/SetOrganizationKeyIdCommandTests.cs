using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.OrganizationKeys.Commands;
using NSubstitute;
using Xunit;

namespace Bit.OrganizationKeys.Test.Commands;

public class SetOrganizationKeyIdCommandTests
{
    private const string _otherKeyId = "fedcba9876543210fedcba9876543210";

    private readonly KeyId _organizationKeyId = KeyId.FromHexEncodedString("0123456789abcdef0123456789abcdef")!;
    private readonly Organization _organization = new() { Id = Guid.NewGuid() };
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly SetOrganizationKeyIdCommand _sut;

    public SetOrganizationKeyIdCommandTests() => _sut = new SetOrganizationKeyIdCommand(_organizationRepository);

    [Fact]
    public async Task SetOrganizationKeyIdAsync_NoKeyIdRecorded_StoresTheKeyId()
    {
        _organizationRepository.SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId).Returns(true);

        await _sut.SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId);

        await _organizationRepository.Received(1).SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId);
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Fact]
    public async Task SetOrganizationKeyIdAsync_SameKeyIdAlreadyRecorded_Succeeds()
    {
        _organization.OrganizationKeyId = _organizationKeyId.ToString();
        _organizationRepository.SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId).Returns(false);
        _organizationRepository.GetByIdAsync(_organization.Id).Returns(_organization);

        await _sut.SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId);

        await _organizationRepository.Received(1).SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId);
        await _organizationRepository.Received(1).GetByIdAsync(_organization.Id);
    }

    [Fact]
    public async Task SetOrganizationKeyIdAsync_DifferentKeyIdAlreadyRecorded_Throws()
    {
        _organization.OrganizationKeyId = _otherKeyId;
        _organizationRepository.SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId).Returns(false);
        _organizationRepository.GetByIdAsync(_organization.Id).Returns(_organization);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _sut.SetOrganizationKeyIdAsync(_organization.Id, _organizationKeyId));

        Assert.Equal("Organization key id is already set.", exception.Message);
    }
}
