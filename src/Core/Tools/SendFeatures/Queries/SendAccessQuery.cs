using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;

namespace Bit.Core.Tools.SendFeatures.Queries;

/// <inheritdoc cref="ISendAccessQuery"/>
public class SendAccessQuery : ISendAccessQuery
{
    private readonly ISendRepository _sendRepository;

    /// <summary>
    /// Instantiates the query
    /// </summary>
    /// <param name="sendRepository">
    /// Retrieves send records
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sendRepository"/> is <see langword="null"/>.
    /// </exception>
    public SendAccessQuery(ISendRepository sendRepository)
    {
        _sendRepository = sendRepository ?? throw new ArgumentNullException(nameof(sendRepository));
    }

    /// <inheritdoc cref="ISendAccessQuery.Get"/>
    public Task<Send?> Get(Guid id) => _sendRepository.GetByIdAsync(id);
}
