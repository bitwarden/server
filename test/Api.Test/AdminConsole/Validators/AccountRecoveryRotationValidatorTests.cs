using Bit.Api.AdminConsole;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Validators;

[SutProviderCustomize]
public class AccountRecoveryRotationValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_MissingAccountRecovery_Throws(
        SutProvider<AccountRecoveryRotationValidator> sutProvider, User user,
        IEnumerable<AccountRecoveryWithIdRequestModel> accountRecoveryKeys)
    {
        var existingUserAccountRecovery = accountRecoveryKeys
            .Select(a => new OrganizationUser { Id = a.Id, ResetPasswordKey = a.ResetPasswordKey }).ToList();
        existingUserAccountRecovery.Add(new OrganizationUser
        {
            Id = Guid.NewGuid(), ResetPasswordKey = "Missing AccountRecoveryKey"
        });
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserAccountRecovery);


        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, accountRecoveryKeys));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AccountRecoveryDoesNotBelongToUser_NotReturned(
        SutProvider<AccountRecoveryRotationValidator> sutProvider, User user,
        IEnumerable<AccountRecoveryWithIdRequestModel> accountRecoveryKeys)
    {
        var existingUserAccountRecovery = accountRecoveryKeys
            .Select(a => new OrganizationUser { Id = a.Id, ResetPasswordKey = a.ResetPasswordKey }).ToList();
        existingUserAccountRecovery.RemoveAt(0);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserAccountRecovery);

        var result = await sutProvider.Sut.ValidateAsync(user, accountRecoveryKeys);

        Assert.DoesNotContain(result, c => c.Id == accountRecoveryKeys.First().Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AttemptToSetKeyToNull_Throws(
        SutProvider<AccountRecoveryRotationValidator> sutProvider, User user,
        IEnumerable<AccountRecoveryWithIdRequestModel> accountRecoveryKeys)
    {
        var existingUserAccountRecovery = accountRecoveryKeys
            .Select(a => new OrganizationUser { Id = a.Id, ResetPasswordKey = a.ResetPasswordKey }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserAccountRecovery);
        accountRecoveryKeys.First().ResetPasswordKey = null;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, accountRecoveryKeys));
    }
}
