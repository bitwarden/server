using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class FillAssistPolicyEventHandlerTests
{
    [Fact]
    public void Type_ReturnsFillAssist()
    {
        var handler = new FillAssistPolicyEventHandler();

        Assert.Equal(PolicyType.FillAssist, handler.Type);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_NullData_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = null;
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal("The RulesUrl field is required to enable the Fill Assist policy.", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_EmptyData_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = string.Empty;
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal("The RulesUrl field is required to enable the Fill Assist policy.", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_WithData_ReturnsEmpty(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"rulesUrl\":\"https://example.com/rules\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal(string.Empty, result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_NullData_ReturnsEmpty(
        [PolicyUpdate(PolicyType.FillAssist, false)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = null;
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal(string.Empty, result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_EmptyData_ReturnsEmpty(
        [PolicyUpdate(PolicyType.FillAssist, false)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = string.Empty;
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal(string.Empty, result);
    }
}
