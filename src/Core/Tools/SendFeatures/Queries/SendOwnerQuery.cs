
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;

#nullable enable

namespace Bit.Core.Tools.SendFeatures.Queries;

/// <inheritdoc cref="ISendOwnerQuery"/>
public class SendOwnerQuery : ISendOwnerQuery
{
    private readonly ISendRepository _repository;
    private readonly IFeatureService _features;
    private readonly ICurrentContext _context;
    private Guid CurrentUserId
    {
        get => _context.UserId ?? throw new BadRequestException("Invalid user.");
    }

    /// <summary>
    /// Instantiates the command
    /// </summary>
    /// <param name="sendRepository">
    /// Retrieves send records
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sendRepository"/> is <see langword="null"/>.
    /// </exception>
    public SendOwnerQuery(ISendRepository sendRepository, IFeatureService features, ICurrentContext context)
    {
        _repository = sendRepository;
        _features = features ?? throw new ArgumentNullException(nameof(features));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc cref="ISendOwnerQuery.Get"/>
    public async Task<Send> Get(Guid id)
    {
        var send = await _repository.GetByIdAsync(id);
        if (send == null || send.UserId != CurrentUserId)
        {
            throw new NotFoundException();
        }

        return send;
    }

    /// <inheritdoc cref="ISendOwnerQuery.GetOwned"/>
    public async Task<ICollection<Send>> GetOwned()
    {
        var sends = await _repository.GetManyByUserIdAsync(CurrentUserId);

        var removeEmailOtp = !_features.IsEnabled(FeatureFlagKeys.PM19051_ListEmailOtpSends);
        if (removeEmailOtp)
        {
            // reify list to avoid invalidating the enumerator
            foreach (var s in sends.Where(s => s.Emails != null).ToList())
            {
                sends.Remove(s);
            }
        }

        return sends;
    }
}
