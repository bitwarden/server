namespace Bit.Core.Tools.Queries.Interfaces;

public interface IGetInactiveTwoFactorQuery
{
    Task<Dictionary<string, string>> GetInactiveTwoFactorAsync();
}
