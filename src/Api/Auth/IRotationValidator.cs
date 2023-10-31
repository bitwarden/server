using Bit.Core.Entities;

namespace Bit.Api.Auth;
public interface IRotationValidator<T, R>
{
    Task<R> ValidateAsync(User user, T data);
}
