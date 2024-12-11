using Microsoft.Extensions.Options;

namespace Bit.Sso.Utilities;

public interface IExtendedOptionsMonitorCache<TOptions> : IOptionsMonitorCache<TOptions>
    where TOptions : class
{
    void AddOrUpdate(string name, TOptions options);
}
