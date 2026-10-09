using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class ScopedApiKeyGroupMemberValidatorTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task ValidateAsync_WhenScopedApiKeyAddsElevatedMember_ReturnsError(
        OrganizationUserType type,
        SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId)
    {
        var member = Arrange(sutProvider, organizationId, type, isScopedApiKey: true);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, null, [member.Id]);

        Assert.IsType<ScopedApiKeyCanOnlyManageUsers>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenScopedApiKeyRemovesElevatedMember_ReturnsError(
        SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId, Guid groupId)
    {
        var admin = Arrange(sutProvider, organizationId, OrganizationUserType.Admin, isScopedApiKey: true);
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(groupId).Returns([admin.Id]);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, groupId, []);

        Assert.IsType<ScopedApiKeyCanOnlyManageUsers>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenScopedApiKeyAddsAndRemovesUsers_ReturnsNull(
        SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId, Guid groupId)
    {
        var addedUser = Member(organizationId, OrganizationUserType.User);
        var removedUser = Member(organizationId, OrganizationUserType.User);
        ArrangeMembers(sutProvider, isScopedApiKey: true, addedUser, removedUser);
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(groupId).Returns([removedUser.Id]);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, groupId, [addedUser.Id]);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenScopedApiKeyKeepsElevatedMember_ReturnsNull(
        SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId, Guid groupId, Guid adminId)
    {
        sutProvider.GetDependency<ICurrentContext>().IsScopedOrganizationApiKey.Returns(true);
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(groupId).Returns([adminId]);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, groupId, [adminId]);

        Assert.Null(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().GetManyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenScopedApiKeyAddsElevatedMemberOfAnotherOrganization_ReturnsNull(
        SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId, Guid otherOrganizationId)
    {
        var member = Arrange(sutProvider, otherOrganizationId, OrganizationUserType.Admin, isScopedApiKey: true);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, null, [member.Id]);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WhenNotScopedApiKey_ReturnsNull(
        SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId, Guid groupId)
    {
        var admin = Arrange(sutProvider, organizationId, OrganizationUserType.Admin, isScopedApiKey: false);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, groupId, [admin.Id]);

        Assert.Null(result);
    }

    private static OrganizationUser Arrange(SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        Guid organizationId, OrganizationUserType type, bool isScopedApiKey)
    {
        var member = Member(organizationId, type);
        ArrangeMembers(sutProvider, isScopedApiKey, member);
        return member;
    }

    private static OrganizationUser Member(Guid organizationId, OrganizationUserType type) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Type = type };

    private static void ArrangeMembers(SutProvider<ScopedApiKeyGroupMemberValidator> sutProvider,
        bool isScopedApiKey, params OrganizationUser[] members)
    {
        sutProvider.GetDependency<ICurrentContext>().IsScopedOrganizationApiKey.Returns(isScopedApiKey);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => members.Where(m => callInfo.Arg<IEnumerable<Guid>>().Contains(m.Id)).ToList());
    }
}
