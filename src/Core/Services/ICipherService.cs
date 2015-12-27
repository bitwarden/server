using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface ICipherService
    {
        Task ImportCiphersAsync(List<Folder> folders, List<Site> sites, IEnumerable<KeyValuePair<int, int>> siteRelationships);
    }
}
