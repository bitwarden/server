using Bit.Core.Vault.Commands;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
public class CreateUserPreferencesCommandTest
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_Success_ReturnsCreatedPreferences(
        SutProvider<CreateUserPreferencesCommand> sutProvider,
        Guid userId,
        string data)
    {
        var result = await sutProvider.Sut.CreateAsync(userId, data);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(data, result.Data);
        Assert.NotEqual(Guid.Empty, result.Id);

        await sutProvider.GetDependency<IUserPreferencesRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<UserPreferences>(p =>
                p.UserId == userId && p.Data == data));
    }
}
