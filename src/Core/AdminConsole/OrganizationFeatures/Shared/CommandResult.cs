namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared;

public record CommandResult(params string[] ErrorMessages)
{
    public virtual bool Success => ErrorMessages.Length == 0;
    public virtual bool HasErrors => ErrorMessages.Length != 0;
}

public record PartialResult<T>(
    IEnumerable<T> SuccessfulInputs,
    IEnumerable<T> FailedInputs,
    params string[] ErrorMessages)
    : CommandResult(ErrorMessages)
{
    public bool PartialSuccess => HasErrors || (FailedInputs.Any() && SuccessfulInputs.Any());
    public override bool Success => SuccessfulInputs.Any() && base.Success && !FailedInputs.Any();
};

public static class CommandResultExtensions
{
    public static CommandResult AppendErrors(this CommandResult result, params string[] errorMessages) =>
        new(errorMessages.Concat(result.ErrorMessages).ToArray());
}

public static class PartialResultExtensions
{
    public static PartialResult<T> AppendErrors<T>(this PartialResult<T> result, params string[] errorMessages) =>
        new(result.SuccessfulInputs, result.FailedInputs, errorMessages.Concat(result.ErrorMessages).ToArray());

    public static PartialResult<T> AppendSuccessfulInputs<T>(this PartialResult<T> result, params T[] successfulInputs) =>
        new(result.SuccessfulInputs.Concat(successfulInputs), result.FailedInputs, result.ErrorMessages);

    public static PartialResult<T> AppendFailedInputs<T>(this PartialResult<T> result, params T[] failedInputs) =>
        new(result.SuccessfulInputs, result.FailedInputs.Concat(failedInputs), result.ErrorMessages);
}
