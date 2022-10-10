using Bit.Core.Commands;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Commands;

[SutProviderCustomize]
public class DeleteOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteUser_Success(SutProvider<DeleteOrganizationUserCommand> sutProvider, Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUserId, deletingUserId);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).DeleteUserAsync(organizationId, organizationUserId, deletingUserId);
    }
}
