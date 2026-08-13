using Bit.Core.Auth.UserFeatures.UserApiKey;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserApiKey;

[SutProviderCustomize]
public class RotateUserApiKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task RotateApiKeyAsync_AssignsNewKey_BumpsDates_AndPersists(
        SutProvider<RotateUserApiKeyCommand> sutProvider, User user)
    {
        var existingKey = user.ApiKey;
        user.LastApiKeyRotationDate = null;

        await sutProvider.Sut.RotateApiKeyAsync(user);

        Assert.NotEqual(existingKey, user.ApiKey);
        Assert.Equal(30, user.ApiKey.Length);
        AssertHelper.AssertRecent(user.RevisionDate);
        Assert.NotNull(user.LastApiKeyRotationDate);
        AssertHelper.AssertRecent(user.LastApiKeyRotationDate.Value);
        Assert.Equal(user.RevisionDate, user.LastApiKeyRotationDate.Value);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }
}
