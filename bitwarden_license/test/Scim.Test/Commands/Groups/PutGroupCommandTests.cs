using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Commands.Groups
{
    [SutProviderCustomize]
    public class PutGroupCommandTests
    {
        [Theory]
        [BitAutoData]
        public async Task PutGroup_Success(SutProvider<PutGroupCommand> sutProvider, Group group, string displayName)
        {
            var scimGroupRequestModel = new ScimGroupRequestModel
            {
                DisplayName = displayName,
                Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
            };

            sutProvider.GetDependency<IGroupRepository>()
                .GetByIdAsync(group.Id)
                .Returns(group);

            var expectedResult = new ScimGroupResponseModel(group);
            expectedResult.DisplayName = displayName;

            var result = await sutProvider.Sut.PutGroupAsync(group.OrganizationId, group.Id, scimGroupRequestModel);

            await sutProvider.GetDependency<IGroupService>().Received(1).SaveAsync(group);
            await sutProvider.GetDependency<IGroupRepository>().Received(0).UpdateUsersAsync(group.Id, Arg.Any<IEnumerable<Guid>>());

            AssertHelper.AssertPropertyEqual(expectedResult, result);
        }

        [Theory]
        [BitAutoData]
        public async Task PutGroup_ChangeMembers_Success(SutProvider<PutGroupCommand> sutProvider, Group group, string displayName, IEnumerable<Guid> membersUserIds)
        {
            var scimGroupRequestModel = new ScimGroupRequestModel
            {
                DisplayName = displayName,
                Members = membersUserIds.Select(uid => new ScimGroupRequestModel.GroupMembersModel { Value = uid.ToString() }).ToList(),
                Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
            };

            sutProvider.GetDependency<IGroupRepository>()
                .GetByIdAsync(group.Id)
                .Returns(group);

            sutProvider.GetDependency<IScimContext>()
                .RequestScimProvider
                .Returns(Core.Enums.ScimProviderType.Okta);

            var expectedResult = new ScimGroupResponseModel(group);
            expectedResult.DisplayName = displayName;

            var result = await sutProvider.Sut.PutGroupAsync(group.OrganizationId, group.Id, scimGroupRequestModel);

            await sutProvider.GetDependency<IGroupService>().Received(1).SaveAsync(group);
            await sutProvider.GetDependency<IGroupRepository>().Received(1).UpdateUsersAsync(group.Id, Arg.Any<IEnumerable<Guid>>());

            AssertHelper.AssertPropertyEqual(expectedResult, result);
        }

        [Theory]
        [BitAutoData]
        public async Task PutGroup_NotFound_Throws(SutProvider<PutGroupCommand> sutProvider, Guid organizationId, Guid groupId, string displayName)
        {
            var scimGroupRequestModel = new ScimGroupRequestModel
            {
                DisplayName = displayName,
                Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
            };

            await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PutGroupAsync(organizationId, groupId, scimGroupRequestModel));
        }

        [Theory]
        [BitAutoData]
        public async Task PutGroup_MismatchingOrganizationId_Throws(SutProvider<PutGroupCommand> sutProvider, Guid organizationId, Guid groupId, string displayName)
        {
            var scimGroupRequestModel = new ScimGroupRequestModel
            {
                DisplayName = displayName,
                Schemas = new List<string> { ScimConstants.Scim2SchemaUser }
            };

            sutProvider.GetDependency<IGroupRepository>()
                .GetByIdAsync(groupId)
                .Returns(new Group
                {
                    Id = groupId,
                    OrganizationId = Guid.NewGuid()
                });

            await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.PutGroupAsync(organizationId, groupId, scimGroupRequestModel));
        }
    }
}
