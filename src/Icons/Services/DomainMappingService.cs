namespace Bit.Icons.Services;

public class DomainMappingService : IDomainMappingService
{
    private readonly Dictionary<string, string> _map = new Dictionary<string, string>
    {
        ["login.yahoo.com"] = "yahoo.com",
        ["accounts.google.com"] = "google.com",
        ["photo.walgreens.com"] = "walgreens.com",
        ["passport.yandex.com"] = "yandex.com",
        // TODO: Add others here
    };

    public string MapDomain(string hostname)
    {
        if (_map.ContainsKey(hostname))
        {
            return _map[hostname];
        }

        return hostname;
    }
}
