using Bit.Icons.Models;

namespace Bit.Icons.Services;

public interface IIconFetchingService
{
    Task<IconResult> GetIconAsync(string domain);
}
