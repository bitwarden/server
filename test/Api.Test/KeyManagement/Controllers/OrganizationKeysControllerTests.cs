using System.Security.Claims;
using Bit.Api.KeyManagement.Controllers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(OrganizationKeysController))]
[SutProviderCustomize]
public class OrganizationKeysControllerTests
{
    private const string _organizationKeyId = "0123456789abcdef0123456789abcdef";

    [Theory, BitAutoData]
    public async Task PostOrganizationKeyIdAsync_NoUserId_ThrowsAndDoesNotCallCommand(
        Guid orgId,
        SutProvider<OrganizationKeysController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).ReturnsNull();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sutProvider.Sut.PostOrganizationKeyIdAsync(orgId, Request()));

        await sutProvider.GetDependency<ISetOrganizationKeyIdCommand>().DidNotReceiveWithAnyArgs()
            .SetOrganizationKeyIdAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task PostOrganizationKeyIdAsync_NoMembership_ThrowsAndDoesNotCallCommand(
        Guid orgId,
        Guid userId,
        SutProvider<OrganizationKeysController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgId, userId)
            .ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PostOrganizationKeyIdAsync(orgId, Request()));

        Assert.Equal("Organization key is not available.", exception.Message);
        await sutProvider.GetDependency<ISetOrganizationKeyIdCommand>().DidNotReceiveWithAnyArgs()
            .SetOrganizationKeyIdAsync(default, default!);
    }

    [Theory]
    [BitAutoData((string?)null)]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task PostOrganizationKeyIdAsync_MemberDoesNotHoldTheOrganizationKey_ThrowsAndDoesNotCallCommand(
        string? organizationUserKey,
        Guid orgId,
        Guid userId,
        OrganizationUser organizationUser,
        SutProvider<OrganizationKeysController> sutProvider)
    {
        organizationUser.Key = organizationUserKey;
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgId, userId)
            .Returns(organizationUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PostOrganizationKeyIdAsync(orgId, Request()));

        Assert.Equal("Organization key is not available.", exception.Message);
        await sutProvider.GetDependency<ISetOrganizationKeyIdCommand>().DidNotReceiveWithAnyArgs()
            .SetOrganizationKeyIdAsync(default, default!);
    }

    [Theory, BitAutoData]
    public async Task PostOrganizationKeyIdAsync_MemberHoldsTheOrganizationKey_CallsCommandWithParsedKeyId(
        Guid orgId,
        Guid userId,
        OrganizationUser organizationUser,
        SutProvider<OrganizationKeysController> sutProvider)
    {
        organizationUser.Key = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgId, userId)
            .Returns(organizationUser);

        await sutProvider.Sut.PostOrganizationKeyIdAsync(orgId, Request());

        await sutProvider.GetDependency<ISetOrganizationKeyIdCommand>().Received(1)
            .SetOrganizationKeyIdAsync(orgId, KeyId.FromHexEncodedString(_organizationKeyId)!);
    }

    private static SetOrganizationKeyIdRequestModel Request() => new() { OrganizationKeyId = _organizationKeyId };
}
