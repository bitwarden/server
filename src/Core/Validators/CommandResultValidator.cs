using Bit.Core.Models.Commands;

namespace Bit.Core.Validators;

public static class CommandResultValidator
{
    public static CommandResult ExecuteValidators(Func<CommandResult>[] validators)
    {
        foreach (var validator in validators)
        {
            var result = validator();

            if (result is not Success)
            {
                return result;
            }
        }

        return new Success();
    }

    public static async Task<CommandResult> ExecuteValidatorAsync(Func<Task<CommandResult>>[] validators)
    {
        foreach (var validator in validators)
        {
            var result = await validator();

            if (result is not Success)
            {
                return result;
            }
        }

        return new Success();
    }
}
