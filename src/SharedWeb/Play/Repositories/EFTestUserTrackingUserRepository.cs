using AutoMapper;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SharedWeb.Play.Repositories;

/// <summary>
/// EntityFramework decorator around the <see cref="Bit.Infrastructure.EntityFramework.Repositories.UserRepository"/> that tracks
/// created Users for seeding.
/// </summary>
public class EFTestUserTrackingUserRepository : UserRepository
{
    private readonly IPlayItemService _playItemService;

    public EFTestUserTrackingUserRepository(
        IPlayItemService playItemService,
        IServiceScopeFactory serviceScopeFactory,
        IMapper mapper)
        : base(serviceScopeFactory, mapper)
    {
        _playItemService = playItemService;
    }

    public override async Task<Core.Entities.User> CreateAsync(Core.Entities.User user)
    {
        var createdUser = await base.CreateAsync(user);
        await _playItemService.Record(createdUser);
        return createdUser;
    }
}
