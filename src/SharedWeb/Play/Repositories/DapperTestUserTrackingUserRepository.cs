using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.SharedWeb.Play.Repositories;

/// <summary>
/// Dapper decorator around the <see cref="Bit.Infrastructure.Dapper.Repositories.UserRepository"/> that tracks
/// created Users for seeding.
/// </summary>
public class DapperTestUserTrackingUserRepository : UserRepository
{
    private readonly IPlayDataService _playDataService;

    public DapperTestUserTrackingUserRepository(
        IPlayDataService playDataService,
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
        : base(globalSettings, dataProtectionProvider)
    {
        _playDataService = playDataService;
    }

    public override async Task<User> CreateAsync(User user)
    {
        var createdUser = await base.CreateAsync(user);

        await _playDataService.Record(createdUser);
        return createdUser;
    }
}
