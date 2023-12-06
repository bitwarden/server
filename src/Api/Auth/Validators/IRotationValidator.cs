using Bit.Core.Entities;

namespace Bit.Api.Auth.Validators;

/// <summary>
/// A consistent interface for domains to validate re-encrypted data before saved to database. Some examples are:<br/>
/// - All available encrypted data is accounted for<br/>
/// - All provided encrypted data belongs to the user
/// </summary>
/// <typeparam name="T">Request model</typeparam>
/// <typeparam name="R">Domain model</typeparam>
public interface IRotationValidator<T, R>
{
    Task<R> ValidateAsync(User user, T data);
}
