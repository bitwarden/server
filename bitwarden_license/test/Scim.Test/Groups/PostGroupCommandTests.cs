using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Context;
using Bit.Scim.Groups;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Groups;

[SutProviderCustomize]
public class PostGroupCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task PostGroup_Success(SutProvider<PostGroupCommand> sutProvider, string displayName, string externalId, Guid organizationId, ICollection<Group> groups)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = externalId,
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var expectedResult = new Group
        {
            OrganizationId = organizationId,
            Name = displayName,
            ExternalId = externalId,
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var group = await sutProvider.Sut.PostGroupAsync(organizationId, scimGroupRequestModel);

        await sutProvider.GetDependency<IGroupService>().Received(1).SaveAsync(group, EventSystemUser.SCIM, null);
        await sutProvider.GetDependency<IGroupRepository>().Received(0).UpdateUsersAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>());

        AssertHelper.AssertPropertyEqual(expectedResult, group, "Id", "CreationDate", "RevisionDate");
    }

    [Theory]
    [BitAutoData]
    public async Task PostGroup_WithMembers_Success(SutProvider<PostGroupCommand> sutProvider, string displayName, string externalId, Guid organizationId, ICollection<Group> groups, IEnumerable<Guid> membersUserIds)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = externalId,
            Members = membersUserIds.Select(uid => new ScimGroupRequestModel.GroupMembersModel { Value = uid.ToString() }).ToList(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        var expectedResult = new Group
        {
            OrganizationId = organizationId,
            Name = displayName,
            ExternalId = externalId
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        sutProvider.GetDependency<IScimContext>()
            .RequestScimProvider
            .Returns(Core.Enums.ScimProviderType.Okta);

        var group = await sutProvider.Sut.PostGroupAsync(organizationId, scimGroupRequestModel);

        await sutProvider.GetDependency<IGroupService>().Received(1).SaveAsync(group, EventSystemUser.SCIM, null);
        await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(Arg.Any<Guid>(), Arg.Is<IEnumerable<Guid>>(arg => arg.All(id => membersUserIds.Contains(id))));

        AssertHelper.AssertPropertyEqual(expectedResult, group, "Id", "CreationDate", "RevisionDate");
    }

    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task PostGroup_NullDisplayName_Throws(string displayName, SutProvider<PostGroupCommand> sutProvider, Guid organizationId)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = Guid.NewGuid().ToString(),
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.PostGroupAsync(organizationId, scimGroupRequestModel));
    }

    [Theory]
    [BitAutoData]
    public async Task PostGroup_ExistingExternalId_Throws(string displayName, SutProvider<PostGroupCommand> sutProvider, Guid organizationId, ICollection<Group> groups)
    {
        var scimGroupRequestModel = new ScimGroupRequestModel
        {
            DisplayName = displayName,
            ExternalId = groups.First().ExternalId,
            Members = new List<ScimGroupRequestModel.GroupMembersModel>(),
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        await Assert.ThrowsAsync<ConflictException>(async () => await sutProvider.Sut.PostGroupAsync(organizationId, scimGroupRequestModel));
    }
}
