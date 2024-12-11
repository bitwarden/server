using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request;

public class UpdateDomainsRequestModel
{
    public IEnumerable<IEnumerable<string>> EquivalentDomains { get; set; }
    public IEnumerable<GlobalEquivalentDomainsType> ExcludedGlobalEquivalentDomains { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.EquivalentDomains =
            EquivalentDomains != null ? JsonSerializer.Serialize(EquivalentDomains) : null;
        existingUser.ExcludedGlobalEquivalentDomains =
            ExcludedGlobalEquivalentDomains != null
                ? JsonSerializer.Serialize(ExcludedGlobalEquivalentDomains)
                : null;
        return existingUser;
    }
}
