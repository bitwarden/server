using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;

public static class ErrorMapper
{

    /// <summary>
    /// Maps the ErrorT to a Bit.Exception class.
    /// </summary>
    /// <param name="error"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Exception MapToBitException<T>(Error<T> error) =>
        error switch
        {
            UserAlreadyExistsError alreadyExistsError => new ConflictException(alreadyExistsError.Message),
            _ => new BadRequestException(error.Message)
        };

    /// <summary>
    /// This maps the ErrorT object to the Bit.Exception class.
    ///
    /// This should be replaced by an IActionResult mapper when possible.
    /// </summary>
    /// <param name="errors"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Exception MapToBitException<T>(ICollection<Error<T>> errors) =>
        errors switch
        {
            not null when errors.Count == 1 => MapToBitException(errors.First()),
            not null when errors.Count > 1 => new BadRequestException(string.Join(' ', errors.Select(e => e.Message))),
            _ => new BadRequestException()
        };
}
