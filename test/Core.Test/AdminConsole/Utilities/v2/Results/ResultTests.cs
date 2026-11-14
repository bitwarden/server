using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Xunit;
using None = OneOf.Types.None;

namespace Bit.Core.Test.AdminConsole.Utilities.v2.Results;

public class ResultTests
{
    [Fact]
    public void ImplicitConversion_FromValue_IsSuccessWithThatValue()
    {
        Result<string, TestError> result = "value";

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
        Assert.Equal("value", result.AsSuccess);
    }

    [Fact]
    public void ImplicitConversion_FromError_IsErrorWithThatError()
    {
        var error = new TestError();

        Result<string, TestError> result = error;

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.AsError);
    }

    [Fact]
    public void ResultWithoutValue_ImplicitConversion_FromNone_IsSuccess()
    {
        Result<TestError> result = new None();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
    }

    [Fact]
    public void ResultWithoutValue_ImplicitConversion_FromError_IsErrorWithThatError()
    {
        var error = new TestError();

        Result<TestError> result = error;

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.AsError);
    }

    private sealed record TestError() : Error("Test error");
}
