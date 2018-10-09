using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface IMaintenanceRepository
    {
        Task UpdateStatisticsAsync();
        Task RebuildIndexesAsync();
    }
}
