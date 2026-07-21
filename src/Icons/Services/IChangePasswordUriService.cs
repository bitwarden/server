namespace Bit.Icons.Services;

public interface IChangePasswordUriService
{
    Task<ChangePasswordUriResult> GetChangePasswordUri(string domain);
}
