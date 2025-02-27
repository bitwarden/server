using Bit.Api.Auth.Models.Request;
using Bit.Api.KeyManagement.Validators;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Validators;

[SutProviderCustomize]
public class EmergencyAccessRotationValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_MissingEmergencyAccess_Throws(
        SutProvider<EmergencyAccessRotationValidator> sutProvider, User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var userEmergencyAccess = emergencyAccessKeys.Select(e => new EmergencyAccessDetails
        {
            Id = e.Id,
            GrantorName = user.Name,
            GrantorEmail = user.Email,
            KeyEncrypted = e.KeyEncrypted,
            Type = e.Type
        }).ToList();
        userEmergencyAccess.Add(new EmergencyAccessDetails { Id = Guid.NewGuid(), KeyEncrypted = "TestKey" });
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(userEmergencyAccess);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, emergencyAccessKeys));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_EmergencyAccessDoesNotBelongToUser_NotIncluded(
        SutProvider<EmergencyAccessRotationValidator> sutProvider, User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var userEmergencyAccess = emergencyAccessKeys.Select(e => new EmergencyAccessDetails
        {
            Id = e.Id,
            GrantorName = user.Name,
            GrantorEmail = user.Email,
            KeyEncrypted = e.KeyEncrypted,
            Type = e.Type
        }).ToList();
        userEmergencyAccess.RemoveAt(0);
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(userEmergencyAccess);

        var result = await sutProvider.Sut.ValidateAsync(user, emergencyAccessKeys);

        Assert.DoesNotContain(result, c => c.Id == emergencyAccessKeys.First().Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_UserNotPremium_Success(
        SutProvider<EmergencyAccessRotationValidator> sutProvider, User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        // We want to allow users who have lost premium to rotate their key for any existing emergency access, as long
        // as we restrict it to existing records and don't let them alter data
        user.Premium = false;
        var userEmergencyAccess = emergencyAccessKeys.Select(e => new EmergencyAccessDetails
        {
            Id = e.Id,
            GrantorName = user.Name,
            GrantorEmail = user.Email,
            KeyEncrypted = e.KeyEncrypted,
            Type = e.Type
        }).ToList();
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(userEmergencyAccess);

        var result = await sutProvider.Sut.ValidateAsync(user, emergencyAccessKeys);

        Assert.Equal(userEmergencyAccess, result);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NonConfirmedEmergencyAccess_NotReturned(
        SutProvider<EmergencyAccessRotationValidator> sutProvider, User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        emergencyAccessKeys.First().KeyEncrypted = null;
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var userEmergencyAccess = emergencyAccessKeys.Select(e => new EmergencyAccessDetails
        {
            Id = e.Id,
            GrantorName = user.Name,
            GrantorEmail = user.Email,
            KeyEncrypted = e.KeyEncrypted,
            Type = e.Type
        }).ToList();
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(userEmergencyAccess);

        var result = await sutProvider.Sut.ValidateAsync(user, emergencyAccessKeys);

        Assert.DoesNotContain(result, c => c.Id == emergencyAccessKeys.First().Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AttemptToSetKeyToNull_Throws(
        SutProvider<EmergencyAccessRotationValidator> sutProvider, User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var userEmergencyAccess = emergencyAccessKeys.Select(e => new EmergencyAccessDetails
        {
            Id = e.Id,
            GrantorName = user.Name,
            GrantorEmail = user.Email,
            KeyEncrypted = e.KeyEncrypted,
            Type = e.Type
        }).ToList();
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(userEmergencyAccess);
        emergencyAccessKeys.First().KeyEncrypted = null;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, emergencyAccessKeys));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_SentKeysAreEmptyButDatabaseIsNot_Throws(
        SutProvider<EmergencyAccessRotationValidator> sutProvider, User user,
        IEnumerable<EmergencyAccessWithIdRequestModel> emergencyAccessKeys)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var userEmergencyAccess = emergencyAccessKeys.Select(e => new EmergencyAccessDetails
        {
            Id = e.Id,
            GrantorName = user.Name,
            GrantorEmail = user.Email,
            KeyEncrypted = e.KeyEncrypted,
            Type = e.Type
        }).ToList();
        sutProvider.GetDependency<IEmergencyAccessRepository>().GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(userEmergencyAccess);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.ValidateAsync(user, Enumerable.Empty<EmergencyAccessWithIdRequestModel>()));
    }
}
