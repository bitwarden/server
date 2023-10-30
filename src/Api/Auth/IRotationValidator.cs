
namespace Bit.Api.Auth;
public interface IRotationValidator<T, R>
{
    Task<R> ValidateAsync(Guid userId, T data);
}
