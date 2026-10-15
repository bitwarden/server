using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

internal static class GroupErrorExtensions
{
    /// <summary>
    /// Maps a validation <see cref="Error"/> to the exception the group commands throw. Not found errors are
    /// thrown without a message, so the response cannot be used to enumerate inaccessible resources.
    /// </summary>
    internal static Exception ToException(this Error error) => error switch
    {
        NotFoundError => new NotFoundException(),
        _ => new BadRequestException(error.Message)
    };
}
