using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

[Route("user-preferences")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.SyncUserPreferences)]
public class UserPreferencesController(
    IUserService userService,
    IUserPreferencesRepository userPreferencesRepository,
    ICreateUserPreferencesCommand createUserPreferencesCommand,
    IUpdateUserPreferencesCommand updateUserPreferencesCommand) : Controller
{
    /// <summary>
    /// Retrieves the current user's synced preferences.
    /// </summary>
    /// <returns>UserPreferencesResponseModel</returns>
    /// <exception cref="NotFoundException">If no preferences exist for the user</exception>
    [HttpGet("")]
    public async Task<UserPreferencesResponseModel> GetAsync()
    {
        var userId = userService.GetProperUserId(User) ?? throw new UnauthorizedAccessException();
        var preferences = await userPreferencesRepository.GetByUserIdAsync(userId);
        return preferences == null ? throw new NotFoundException() : new UserPreferencesResponseModel(preferences);
    }

    /// <summary>
    /// Creates synced preferences for the current user.
    /// </summary>
    /// <param name="request">The encrypted preferences data</param>
    /// <returns>UserPreferencesResponseModel</returns>
    [HttpPost("")]
    public async Task<UserPreferencesResponseModel> CreateAsync([FromBody] UpdateUserPreferencesRequestModel request)
    {
        var userId = userService.GetProperUserId(User) ?? throw new UnauthorizedAccessException();
        var preferences = await createUserPreferencesCommand.CreateAsync(userId, request.Data);
        return new UserPreferencesResponseModel(preferences);
    }

    /// <summary>
    /// Updates the current user's synced preferences.
    /// </summary>
    /// <param name="request">The encrypted preferences data</param>
    /// <returns>UserPreferencesResponseModel</returns>
    /// <exception cref="NotFoundException">If no preferences exist for the user</exception>
    [HttpPut("")]
    public async Task<UserPreferencesResponseModel> UpdateAsync([FromBody] UpdateUserPreferencesRequestModel request)
    {
        var userId = userService.GetProperUserId(User) ?? throw new UnauthorizedAccessException();
        var preferences = await updateUserPreferencesCommand.UpdateAsync(userId, request.Data);
        return new UserPreferencesResponseModel(preferences);
    }

    /// <summary>
    /// Deletes the current user's synced preferences.
    /// </summary>
    [HttpDelete("")]
    public async Task DeleteAsync()
    {
        var userId = userService.GetProperUserId(User) ?? throw new UnauthorizedAccessException();
        await userPreferencesRepository.DeleteByUserIdAsync(userId);
    }
}
