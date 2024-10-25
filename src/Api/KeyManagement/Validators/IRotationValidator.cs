using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Api.KeyManagement.Validators;

/// <summary>
/// A consistent interface for domains to validate re-encrypted data before saved to database. Some examples are:<br/>
/// - All available encrypted data is accounted for<br/>
/// - All provided encrypted data belongs to the user
/// </summary>
/// <typeparam name="T">Request model</typeparam>
/// <typeparam name="R">Domain model</typeparam>
public interface IRotationValidator<T, R>
{
    /// <summary>
    /// Validates re-encrypted data before being saved to database.
    /// </summary>
    /// <param name="user">Request model</param>
    /// <param name="data">Domain model</param>
    /// <exception cref="BadRequestException">Throws if data fails validation</exception>
    Task<R> ValidateAsync(User user, T data);
}
