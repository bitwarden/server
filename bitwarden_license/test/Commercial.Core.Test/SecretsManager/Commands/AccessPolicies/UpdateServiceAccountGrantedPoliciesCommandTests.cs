#nullable enable
using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class UpdateServiceAccountGrantedPoliciesCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NoUpdates_DoesNotCallRepository(
        SutProvider<UpdateServiceAccountGrantedPoliciesCommand> sutProvider,
        ServiceAccountGrantedPoliciesUpdates data)
    {
        data.ProjectGrantedPolicyUpdates = [];
        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider.GetDependency<IAccessPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpdateServiceAccountGrantedPoliciesAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_HasUpdates_CallsRepository(
        SutProvider<UpdateServiceAccountGrantedPoliciesCommand> sutProvider,
        ServiceAccountGrantedPoliciesUpdates data)
    {
        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider.GetDependency<IAccessPolicyRepository>()
            .Received(1)
            .UpdateServiceAccountGrantedPoliciesAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }
}
