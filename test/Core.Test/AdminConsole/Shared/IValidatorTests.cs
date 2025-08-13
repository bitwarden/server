using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.AdminConsole.Utilities.Validation;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Shared;

public class IValidatorTests
{
    public class TestClass
    {
        public string Name { get; set; } = string.Empty;
    }

    public record InvalidRequestError<T>(T ErroredValue) : Error<T>(Code, ErroredValue)
    {
        public const string Code = "InvalidRequest";
    }

    public class TestClassValidator : IValidator<TestClass>
    {
        public Task<ValidationResult<TestClass>> ValidateAsync(TestClass value)
        {
            if (string.IsNullOrWhiteSpace(value.Name))
            {
                return Task.FromResult<ValidationResult<TestClass>>(
                    new Invalid<TestClass>(new InvalidRequestError<TestClass>(value)));
            }

            return Task.FromResult<ValidationResult<TestClass>>(new Valid<TestClass>(value));
        }
    }

    [Fact]
    public async Task ValidateAsync_WhenSomethingIsInvalid_ReturnsInvalidWithError()
    {
        var example = new TestClass();

        var result = await new TestClassValidator().ValidateAsync(example);

        Assert.IsType<Invalid<TestClass>>(result);
        var invalidResult = result as Invalid<TestClass>;
        Assert.Equal(InvalidRequestError<TestClass>.Code, invalidResult!.Error.Message);
    }

    [Fact]
    public async Task ValidateAsync_WhenIsValid_ReturnsValid()
    {
        var example = new TestClass { Name = "Valid" };

        var result = await new TestClassValidator().ValidateAsync(example);

        Assert.IsType<Valid<TestClass>>(result);
        var validResult = result as Valid<TestClass>;
        Assert.Equal(example.Name, validResult.Value.Name);
    }
}
