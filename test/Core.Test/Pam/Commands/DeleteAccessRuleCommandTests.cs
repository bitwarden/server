using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.OrganizationFeatures.Commands;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

[SutProviderCustomize]
public class DeleteAccessRuleCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_Deletes(
        AccessRule existing, SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id);

        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1).DeleteAsync(existing);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_MissingExisting_ThrowsNotFound(
        SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((AccessRule?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(
        AccessRule existing, SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), existing.Id));
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }
}
