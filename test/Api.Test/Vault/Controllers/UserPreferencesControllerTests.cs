using System.Security.Claims;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.Controllers;

[ControllerCustomize(typeof(UserPreferencesController))]
[SutProviderCustomize]
public class UserPreferencesControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Get_PreferencesExist_ReturnsPreferences(
        SutProvider<UserPreferencesController> sutProvider,
        Guid userId)
    {
        var preferences = UserPreferences.Create(userId, "encrypted-data");

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        sutProvider.GetDependency<IUserPreferencesRepository>()
            .GetByUserIdAsync(userId)
            .Returns(preferences);

        var result = await sutProvider.Sut.GetAsync();

        Assert.NotNull(result);
        Assert.Equal(preferences.Data, result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_PreferencesNotFound_ThrowsNotFoundException(
        SutProvider<UserPreferencesController> sutProvider,
        Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        sutProvider.GetDependency<IUserPreferencesRepository>()
            .GetByUserIdAsync(userId)
            .Returns((UserPreferences?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync());
    }

    [Theory]
    [BitAutoData]
    public async Task Create_Success_ReturnsCreatedPreferences(
        SutProvider<UserPreferencesController> sutProvider,
        Guid userId,
        string data)
    {
        var model = new UpdateUserPreferencesRequestModel { Data = data };
        var preferences = UserPreferences.Create(userId, data);

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        sutProvider.GetDependency<ICreateUserPreferencesCommand>()
            .CreateAsync(userId, data)
            .Returns(preferences);

        var result = await sutProvider.Sut.CreateAsync(model);

        Assert.NotNull(result);
        Assert.Equal(data, result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task Update_Success_ReturnsUpdatedPreferences(
        SutProvider<UserPreferencesController> sutProvider,
        Guid userId,
        string data)
    {
        var model = new UpdateUserPreferencesRequestModel { Data = data };
        var preferences = UserPreferences.Create(userId, data);

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        sutProvider.GetDependency<IUpdateUserPreferencesCommand>()
            .UpdateAsync(userId, data)
            .Returns(preferences);

        var result = await sutProvider.Sut.UpdateAsync(model);

        Assert.NotNull(result);
        Assert.Equal(data, result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task Delete_Success_CallsRepository(
        SutProvider<UserPreferencesController> sutProvider,
        Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        await sutProvider.Sut.DeleteAsync();

        await sutProvider.GetDependency<IUserPreferencesRepository>()
            .Received(1)
            .DeleteByUserIdAsync(userId);
    }
}
