using Bit.Api.AdminConsole.Public.Controllers;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Controllers;

[ControllerCustomize(typeof(PoliciesController))]
[SutProviderCustomize]
public class PoliciesControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Put_UsesVNextSavePolicyCommand(
        Guid organizationId,
        PolicyType policyType,
        PolicyUpdateRequestModel model,
        Policy policy,
        SutProvider<PoliciesController> sutProvider)
    {
        // Arrange
        policy.Data = null;
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationId.Returns(organizationId);
        sutProvider.GetDependency<IVNextSavePolicyCommand>()
            .SaveAsync(Arg.Any<SavePolicyModel>())
            .Returns(policy);

        // Act
        await sutProvider.Sut.Put(policyType, model);

        // Assert
        await sutProvider.GetDependency<IVNextSavePolicyCommand>()
            .Received(1)
            .SaveAsync(Arg.Is<SavePolicyModel>(m =>
                m.PolicyUpdate.OrganizationId == organizationId &&
                m.PolicyUpdate.Type == policyType &&
                m.PolicyUpdate.Enabled == model.Enabled.GetValueOrDefault() &&
                m.PerformedBy is SystemUser));
    }
}
