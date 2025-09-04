namespace Bit.Icons.Services;

public interface IChangePasswordUriService
{
    Task<string?> GetChangePasswordUri(string domain);
}
