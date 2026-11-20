using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.CurrentUser;
using Bit.OrganizationKeys.Handlers;
using Bit.OrganizationKeys.Models.Requests;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.OrganizationKeys.Test.Handlers;

public class SetOrganizationKeyIdHandlerTests
{
    private const string _organizationKeyId = "0123456789abcdef0123456789abcdef";
    private const string _wrappedOrganizationKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();
    private readonly FakeSetOrganizationKeyIdCommand _command = new();
    private readonly SetOrganizationKeyIdHandler _sut;

    public SetOrganizationKeyIdHandlerTests()
    {
        _currentUserContext.UserId.Returns(_userId);
        _sut = new SetOrganizationKeyIdHandler(_currentUserContext, _organizationUserRepository, _command);
    }

    [Fact]
    public async Task HandleAsync_NoMembership_ThrowsWithoutCallingCommand()
    {
        _organizationUserRepository.GetByOrganizationAsync(_organizationId, _userId).ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.HandleAsync(_organizationId, Request(_organizationKeyId)));

        Assert.Equal("Organization key is not available.", exception.Message);
        Assert.False(_command.Called);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task HandleAsync_MemberDoesNotHoldTheOrganizationKey_ThrowsWithoutCallingCommand(
        string? organizationUserKey)
    {
        _organizationUserRepository.GetByOrganizationAsync(_organizationId, _userId)
            .Returns(new OrganizationUser { OrganizationId = _organizationId, UserId = _userId, Key = organizationUserKey });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.HandleAsync(_organizationId, Request(_organizationKeyId)));

        Assert.Equal("Organization key is not available.", exception.Message);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_MemberHoldsTheOrganizationKey_CallsCommandWithParsedKeyId()
    {
        _organizationUserRepository.GetByOrganizationAsync(_organizationId, _userId)
            .Returns(new OrganizationUser
            {
                OrganizationId = _organizationId,
                UserId = _userId,
                Key = _wrappedOrganizationKey
            });

        await _sut.HandleAsync(_organizationId, Request(_organizationKeyId));

        Assert.True(_command.Called);
        Assert.Equal(_organizationId, _command.ReceivedOrganizationId);
        Assert.Equal(KeyId.FromHexEncodedString(_organizationKeyId), _command.ReceivedOrganizationKeyId);
    }

    private static SetOrganizationKeyIdRequest Request(string organizationKeyId) =>
        new() { OrganizationKeyId = organizationKeyId };
}
