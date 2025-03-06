using Bit.Core.Models.Commands;
using Bit.Core.Validators;
using Xunit;

namespace Bit.Core.Test.Validators;

public class CommandResultValidatorTests
{
    [Fact]
    public void ExecuteValidators_AllSuccess_ReturnsSuccess()
    {
        // Arrange
        var validators = new Func<CommandResult>[]
        {
            () => new Success(),
            () => new Success(),
            () => new Success()
        };

        // Act
        var result = CommandResultValidator.ExecuteValidators(validators);

        // Assert
        Assert.IsType<Success>(result);
    }

    public static IEnumerable<object[]> TestCases()
    {
        yield return new object[]
        {
        new Func<CommandResult>[]
        {
            () => new Failure("First failure"),
            () => new Success(),
            () => new Failure("Second failure"),
        }
        };
        yield return new object[]
        {
        new Func<CommandResult>[]
        {
            () => new Success(),
            () => new Failure("First failure"),
            () => new Failure("Second failure"),
        }
        };
        yield return new object[]
        {
        new Func<CommandResult>[]
        {
            () => new Success(),
            () => new Success(),
            () => new Failure("First failure"),
        }
        };
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void ExecuteValidators_WhenValidatorFails_ReturnsFirstFailure(Func<CommandResult>[] validators)
    {
        // Act
        var result = CommandResultValidator.ExecuteValidators(validators);

        // Assert
        Assert.IsType<Failure>(result);
        Assert.Equal(["First failure"], ((Failure)result).ErrorMessages);
    }

    [Fact]
    public async Task ExecuteValidatorAsync_AllSuccess_ReturnsSuccess()
    {
        // Arrange
        var validators = new Func<Task<CommandResult>>[]
        {
            async () => await Task.FromResult(new Success()),
            async () => await Task.FromResult(new Success()),
            async () => await Task.FromResult(new Success())
        };

        // Act
        var result = await CommandResultValidator.ExecuteValidatorAsync(validators);

        // Assert
        Assert.IsType<Success>(result);
    }

    public static IEnumerable<object[]> AsyncTestCases()
    {
        yield return new object[]
        {
            new Func<Task<CommandResult>>[]
            {
                async () => await Task.FromResult(new Failure("First failure")),
                async () => await Task.FromResult(new Success()),
                async () => await Task.FromResult(new Failure("Second failure")),
            }
        };
        yield return new object[]
        {
            new Func<Task<CommandResult>>[]
            {
                async () => await Task.FromResult(new Success()),
                async () => await Task.FromResult(new Failure("First failure")),
                async () => await Task.FromResult(new Failure("Second failure")),
            }
        };
        yield return new object[]
        {
            new Func<Task<CommandResult>>[]
            {
                async () => await Task.FromResult(new Success()),
                async () => await Task.FromResult(new Success()),
                async () => await Task.FromResult(new Failure("First failure")),
            }
        };
    }

    [Theory]
    [MemberData(nameof(AsyncTestCases))]
    public async Task ExecuteValidatorAsync_WhenValidatorFails_ReturnsFirstFailure(Func<Task<CommandResult>>[] validators)
    {
        // Act
        var result = await CommandResultValidator.ExecuteValidatorAsync(validators);

        // Assert
        Assert.IsType<Failure>(result);
        Assert.Equal(["First failure"], ((Failure)result).ErrorMessages);
    }
}
