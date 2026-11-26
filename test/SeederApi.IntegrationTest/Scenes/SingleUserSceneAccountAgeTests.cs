using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SingleUserSceneAccountAgeTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private User? _saved;

    public SingleUserSceneAccountAgeTests()
    {
        _userRepository.CreateAsync(Arg.Do<User>(u => _saved = u));
    }

    [Fact]
    public async Task SeedAsync_DefaultAccountAge_CreatesPresentDayAccount()
    {
        var before = DateTime.UtcNow;

        await CreateScene().SeedAsync(NewRequest());

        Assert.InRange(_saved!.CreationDate, before, DateTime.UtcNow);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public async Task SeedAsync_AccountAgeDays_BackdatesCreationDate(int days)
    {
        var before = DateTime.UtcNow;

        await CreateScene().SeedAsync(NewRequest(days));

        var after = DateTime.UtcNow;
        Assert.InRange(_saved!.CreationDate, before.AddDays(-days), after.AddDays(-days));
        Assert.InRange(_saved.RevisionDate, before, after);
        Assert.InRange(_saved.AccountRevisionDate, before, after);
    }

    [Fact]
    public async Task SeedAsync_PremiumWithAccountAgeDays_KeepsPremiumExpirationRelativeToNow()
    {
        var before = DateTime.UtcNow;
        var request = NewRequest(30);
        request.Premium = true;

        await CreateScene().SeedAsync(request);

        var after = DateTime.UtcNow;
        Assert.InRange(_saved!.CreationDate, before.AddDays(-30), after.AddDays(-30));
        Assert.True(_saved.Premium);
        Assert.InRange(_saved.PremiumExpirationDate!.Value, before.AddYears(1), after.AddYears(1));
    }

    [Fact]
    public async Task SeedAsync_NegativeAccountAgeDays_ThrowsWithoutCreatingUser()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateScene().SeedAsync(NewRequest(-1)));

        await _userRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    private SingleUserScene CreateScene() => new(
        new PasswordHasher<User>(),
        _userRepository,
        new NoOpManglerService(),
        () => Substitute.For<ILicensingService>(),
        Substitute.For<ISeederLicenseSigner>(),
        Substitute.For<IGlobalSettings>(),
        NullLogger<SingleUserScene>.Instance);

    private static SingleUserScene.Request NewRequest(int accountAgeDays = 0) => new()
    {
        Email = "aged-user@example.com",
        Password = "asdfasdfasdf",
        AccountAgeDays = accountAgeDays
    };
}
