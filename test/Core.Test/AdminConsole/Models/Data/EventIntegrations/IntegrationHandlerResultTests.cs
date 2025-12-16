using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Data.EventIntegrations;

public class IntegrationHandlerResultTests
{
    [Theory, BitAutoData]
    public void Succeed_SetsSuccessTrue_CategoryNull(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Succeed(message);

        Assert.True(result.Success);
        Assert.Null(result.Category);
        Assert.Equal(message, result.Message);
        Assert.Null(result.FailureReason);
    }

    [Theory, BitAutoData]
    public void Fail_WithCategory_SetsSuccessFalse_CategorySet(IntegrationMessage message)
    {
        var category = IntegrationFailureCategory.AuthenticationFailed;
        var failureReason = "Invalid credentials";

        var result = IntegrationHandlerResult.Fail(message, category, failureReason);

        Assert.False(result.Success);
        Assert.Equal(category, result.Category);
        Assert.Equal(failureReason, result.FailureReason);
        Assert.Equal(message, result.Message);
    }

    [Theory, BitAutoData]
    public void Fail_WithDelayUntil_SetsDelayUntilDate(IntegrationMessage message)
    {
        var delayUntil = DateTime.UtcNow.AddMinutes(5);

        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.RateLimited,
            "Rate limited",
            delayUntil
        );

        Assert.Equal(delayUntil, result.DelayUntilDate);
    }

    [Theory, BitAutoData]
    public void Retryable_RateLimited_ReturnsTrue(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.RateLimited,
            "Rate limited"
        );

        Assert.True(result.Retryable);
    }

    [Theory, BitAutoData]
    public void Retryable_TransientError_ReturnsTrue(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.TransientError,
            "Temporary network issue"
        );

        Assert.True(result.Retryable);
    }

    [Theory, BitAutoData]
    public void Retryable_AuthenticationFailed_ReturnsFalse(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.AuthenticationFailed,
            "Invalid token"
        );

        Assert.False(result.Retryable);
    }

    [Theory, BitAutoData]
    public void Retryable_ConfigurationError_ReturnsFalse(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.ConfigurationError,
            "Channel not found"
        );

        Assert.False(result.Retryable);
    }

    [Theory, BitAutoData]
    public void Retryable_ServiceUnavailable_ReturnsTrue(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.ServiceUnavailable,
            "Service is down"
        );

        Assert.True(result.Retryable);
    }

    [Theory, BitAutoData]
    public void Retryable_PermanentFailure_ReturnsFalse(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Fail(
            message,
            IntegrationFailureCategory.PermanentFailure,
            "Permanent failure"
        );

        Assert.False(result.Retryable);
    }

    [Theory, BitAutoData]
    public void Retryable_SuccessCase_ReturnsFalse(IntegrationMessage message)
    {
        var result = IntegrationHandlerResult.Succeed(message);

        Assert.False(result.Retryable);
    }
}
