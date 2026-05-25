using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Commands;

[SutProviderCustomize]
public class DeleteLeasingPolicyCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_Deletes(
        LeasingPolicy existing, SutProvider<DeleteLeasingPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id);

        await sutProvider.GetDependency<ILeasingPolicyRepository>().Received(1).DeleteAsync(existing);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_MissingExisting_ThrowsNotFound(
        SutProvider<DeleteLeasingPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((LeasingPolicy?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
        await sutProvider.GetDependency<ILeasingPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(
        LeasingPolicy existing, SutProvider<DeleteLeasingPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), existing.Id));
        await sutProvider.GetDependency<ILeasingPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }
}
