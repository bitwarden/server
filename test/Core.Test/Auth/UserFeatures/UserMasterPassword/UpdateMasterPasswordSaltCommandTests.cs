using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class UpdateMasterPasswordSaltCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_UserNotFound_DoesNotWrite(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).ReturnsNull();

        await sutProvider.Sut.UpdateAsync(userId);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_SaltAlreadySet_DoesNotWrite(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        user.MasterPassword = "hashed-master-password";
        user.MasterPasswordSalt = "existing-salt";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);

        await sutProvider.Sut.UpdateAsync(user.Id);

        Assert.Equal("existing-salt", user.MasterPasswordSalt);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NoMasterPassword_DoesNotWrite(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        // Key Connector / TDE users have no master password, so there is no salt to prefill.
        user.MasterPassword = null;
        user.MasterPasswordSalt = null;
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);

        await sutProvider.Sut.UpdateAsync(user.Id);

        Assert.Null(user.MasterPasswordSalt);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_SaltUnset_WritesNormalizedEmail(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        user.Email = "  MiXeD.CaSe@Example.COM  ";
        user.MasterPassword = "hashed-master-password";
        user.MasterPasswordSalt = null;
        var revisionDate = user.RevisionDate;
        var accountRevisionDate = user.AccountRevisionDate;
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);

        await sutProvider.Sut.UpdateAsync(user.Id);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.Id == user.Id && u.MasterPasswordSalt == "mixed.case@example.com"));

        // The prefilled value matches what clients already derive, so there is nothing to re-sync.
        Assert.Equal(revisionDate, user.RevisionDate);
        Assert.Equal(accountRevisionDate, user.AccountRevisionDate);
    }
}
