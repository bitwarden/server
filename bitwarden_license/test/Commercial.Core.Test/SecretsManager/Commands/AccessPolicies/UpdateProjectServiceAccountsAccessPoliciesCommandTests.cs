using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class UpdateProjectServiceAccountsAccessPoliciesCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NoUpdates_DoesNotCallRepository(
        SutProvider<UpdateProjectServiceAccountsAccessPoliciesCommand> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates data
    )
    {
        data.ServiceAccountAccessPolicyUpdates = [];
        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider
            .GetDependency<IAccessPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpdateProjectServiceAccountsAccessPoliciesAsync(
                Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>()
            );
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_HasUpdates_CallsRepository(
        SutProvider<UpdateProjectServiceAccountsAccessPoliciesCommand> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates data
    )
    {
        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider
            .GetDependency<IAccessPolicyRepository>()
            .Received(1)
            .UpdateProjectServiceAccountsAccessPoliciesAsync(
                Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>()
            );
    }
}
