using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class UpdateMasterPasswordSaltCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_SaltAlreadySet_DoesNotCallRepository(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        user.MasterPassword = "hashed-master-password";
        user.MasterPasswordSalt = "existing-salt";

        await sutProvider.Sut.UpdateAsync(user);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .SetMasterPasswordSaltIfNullAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_NoMasterPassword_DoesNotCallRepository(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        // Key Connector / TDE users have no master password, so there is no salt to prefill.
        user.MasterPassword = null;
        user.MasterPasswordSalt = null;

        await sutProvider.Sut.UpdateAsync(user);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .SetMasterPasswordSaltIfNullAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_SaltUnset_PassesNormalizedEmail(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        user.Email = "  MiXeD.CaSe@Example.COM  ";
        user.MasterPassword = "hashed-master-password";
        user.MasterPasswordSalt = null;

        await sutProvider.Sut.UpdateAsync(user);

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .SetMasterPasswordSaltIfNullAsync(user.Id, "mixed.case@example.com");
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotMutateOrPersistTheEntity(
        SutProvider<UpdateMasterPasswordSaltCommand> sutProvider, User user)
    {
        // The entity is a read-only input: on the refresh path it is the instance shared on
        // ICurrentContext, and the salt is written by a conditional UPDATE rather than a save.
        // Re-reading the row here would also defeat reusing the already-resolved entity.
        user.MasterPassword = "hashed-master-password";
        user.MasterPasswordSalt = null;
        var revisionDate = user.RevisionDate;
        var accountRevisionDate = user.AccountRevisionDate;

        await sutProvider.Sut.UpdateAsync(user);

        Assert.Null(user.MasterPasswordSalt);
        Assert.Equal(revisionDate, user.RevisionDate);
        Assert.Equal(accountRevisionDate, user.AccountRevisionDate);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }
}
