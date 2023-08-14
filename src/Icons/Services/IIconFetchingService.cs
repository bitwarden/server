#nullable enable

using Bit.Icons.Models;

namespace Bit.Icons.Services;

public interface IIconFetchingService
{
    Task<Icon?> GetIconAsync(string domain);
}
