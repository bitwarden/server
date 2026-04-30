
using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Queries.Interfaces;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReceiveFeatures.Queries;

/// <inheritdoc cref="IReceiveOwnerQuery"/>
public class ReceiveOwnerQuery : IReceiveOwnerQuery
{
    private readonly IReceiveRepository _repository;
    private readonly IUserService _users;

    /// <summary>
    /// Instantiates the query.
    /// </summary>
    /// <param name="receiveRepository">
    /// Retrieves receive records.
    /// </param>
    /// <param name="users">
    /// Resolves the current user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="receiveRepository"/> or <paramref name="users"/> is <see langword="null"/>.
    /// </exception>
    public ReceiveOwnerQuery(IReceiveRepository receiveRepository, IUserService users)
    {
        _repository = receiveRepository ?? throw new ArgumentNullException(nameof(receiveRepository));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <inheritdoc cref="IReceiveOwnerQuery.Get"/>
    public async Task<Receive> Get(Guid id, ClaimsPrincipal user)
    {
        var userId = _users.GetProperUserId(user) ?? throw new BadRequestException("invalid user.");
        var receive = await _repository.GetByIdAsync(id);

        if (receive == null || receive.UserId != userId)
        {
            throw new NotFoundException();
        }

        return receive;
    }

    /// <inheritdoc cref="IReceiveOwnerQuery.GetOwned"/>
    public async Task<ICollection<Receive>> GetOwned(ClaimsPrincipal user)
    {
        var userId = _users.GetProperUserId(user) ?? throw new BadRequestException("invalid user.");
        var receives = await _repository.GetManyByUserIdAsync(userId);

        return receives;
    }
}
