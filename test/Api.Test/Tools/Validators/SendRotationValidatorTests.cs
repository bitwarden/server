
using Bit.Api.Tools;
using Bit.Api.Tools.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Validators;

[SutProviderCustomize]
public class SendRotationValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_MissingSend_Throws(SutProvider<SendRotationValidator> sutProvider, User user, IEnumerable<SendWithIdRequestModel> sends)
    {
        var userSends = sends.Select(c => new Send { Id = c.Id.GetValueOrDefault() }).ToList();
        userSends.Add(new Send { Id = Guid.NewGuid() });
        sutProvider.GetDependency<ISendRepository>().GetManyByUserIdAsync(user.Id).Returns(userSends);


        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.ValidateAsync(user, sends));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_SendDoesNotBelongToUser_NotIncluded(SutProvider<SendRotationValidator> sutProvider, User user, IEnumerable<SendWithIdRequestModel> sends)
    {
        var userSends = sends.Select(c => new Send { Id = c.Id.GetValueOrDefault() }).ToList();
        userSends.RemoveAt(0);
        sutProvider.GetDependency<ISendRepository>().GetManyByUserIdAsync(user.Id).Returns(userSends);

        var result = await sutProvider.Sut.ValidateAsync(user, sends);

        Assert.DoesNotContain(result, c => c.Id == sends.First().Id);
    }
}
