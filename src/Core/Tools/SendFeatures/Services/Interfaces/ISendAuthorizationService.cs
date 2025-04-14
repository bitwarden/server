using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface ISendAuthorizationService
{
    Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password);
    public (bool grant, bool passwordRequiredError, bool passwordInvalidError) SendCanBeAccessed(Send send,
        string password);
    string HashPassword(string password);
}
