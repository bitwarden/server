using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Commands.Groups;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Commands.Groups
{
    [SutProviderCustomize]
    public class GetGroupCommandTests
    {
        [Theory]
        [BitAutoData]
        public async Task GetGroup_Success(SutProvider<GetGroupCommand> sutProvider, Group group)
        {
            var expectedResult = new Models.ScimGroupResponseModel(group);

            sutProvider.GetDependency<IGroupRepository>()
                .GetByIdAsync(group.Id)
                .Returns(group);

            var result = await sutProvider.Sut.GetGroupAsync(group.OrganizationId, group.Id);

            await sutProvider.GetDependency<IGroupRepository>().Received(1).GetByIdAsync(group.Id);
            AssertHelper.AssertPropertyEqual(expectedResult, result);
        }

        [Theory]
        [BitAutoData]
        public async Task GetUser_NotFound_Throws(SutProvider<GetGroupCommand> sutProvider, Guid organizationId, Guid groupId)
        {
            await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetGroupAsync(organizationId, groupId));
        }

        [Theory]
        [BitAutoData]
        public async Task GetUser_MismatchingOrganizationId_Throws(SutProvider<GetGroupCommand> sutProvider, Guid organizationId, Guid groupId)
        {
            sutProvider.GetDependency<IGroupRepository>()
                .GetByIdAsync(groupId)
                .Returns(new Group
                {
                    Id = groupId,
                    OrganizationId = Guid.NewGuid()
                });

            await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetGroupAsync(organizationId, groupId));
        }
    }
}
