using Bit.Core.Exceptions;
using Bit.Core.Vault.Commands;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
public class UpdateUserPreferencesCommandTest
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_PreferencesNotFound_ThrowsNotFoundException(
        SutProvider<UpdateUserPreferencesCommand> sutProvider,
        Guid userId,
        string data)
    {
        sutProvider.GetDependency<IUserPreferencesRepository>()
            .GetByUserIdAsync(userId)
            .Returns((UserPreferences?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateAsync(userId, data));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Success_UpdatesAndReturnsPreferences(
        SutProvider<UpdateUserPreferencesCommand> sutProvider,
        Guid userId,
        string data)
    {
        var existing = UserPreferences.Create(userId, "old-data");

        sutProvider.GetDependency<IUserPreferencesRepository>()
            .GetByUserIdAsync(userId)
            .Returns(existing);

        var result = await sutProvider.Sut.UpdateAsync(userId, data);

        Assert.NotNull(result);
        Assert.Equal(data, result.Data);

        await sutProvider.GetDependency<IUserPreferencesRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<UserPreferences>(p => p.Data == data));
    }
}
