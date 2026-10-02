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

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_MalformedUrlIgnored_ReturnsEmpty(
        [PolicyUpdate(PolicyType.FillAssist, false)] PolicyUpdate policyUpdate)
    {
        // Disable path skips URL validation entirely; any RulesUrl value is accepted.
        policyUpdate.Data = "{\"rulesUrl\":\"not a url\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal(string.Empty, result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_NullData_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = null;
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.NotEqual(string.Empty, result);
        Assert.Contains("RulesUrl", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_EmptyData_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = string.Empty;
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.NotEqual(string.Empty, result);
        Assert.Contains("RulesUrl", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_MissingRulesUrlKey_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"unrelated\":\"value\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.NotEqual(string.Empty, result);
        Assert.Contains("RulesUrl", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_EmptyRulesUrl_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"rulesUrl\":\"\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.NotEqual(string.Empty, result);
        Assert.Contains("RulesUrl", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_MalformedUrl_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"rulesUrl\":\"not a url\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.NotEqual(string.Empty, result);
        Assert.Contains("RulesUrl", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_HttpUrl_ReturnsError(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"rulesUrl\":\"http://example.com/rules\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.NotEqual(string.Empty, result);
        Assert.Contains("HTTPS", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_ValidHttpsUrl_ReturnsEmpty(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"rulesUrl\":\"https://example.com/rules\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal(string.Empty, result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_UppercaseHttpsUrl_ReturnsEmpty(
        [PolicyUpdate(PolicyType.FillAssist, true)] PolicyUpdate policyUpdate)
    {
        policyUpdate.Data = "{\"rulesUrl\":\"HTTPS://example.com/rules\"}";
        var handler = new FillAssistPolicyEventHandler();

        var result = await handler.ValidateAsync(new SavePolicyModel(policyUpdate), null);

        Assert.Equal(string.Empty, result);
    }
}
