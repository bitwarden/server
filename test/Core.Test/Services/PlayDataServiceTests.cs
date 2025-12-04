using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class PlayDataServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task Record_User_WhenInPlay_RecordsPlayData(
        string playId,
        User user,
        SutProvider<PlayDataService> sutProvider)
    {
        sutProvider.GetDependency<IPlayIdService>()
            .InPlay(out Arg.Any<string>())
            .Returns(x =>
            {
                x[0] = playId;
                return true;
            });

        await sutProvider.Sut.Record(user);

        await sutProvider.GetDependency<IPlayDataRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<PlayData>(pd =>
                pd.PlayId == playId &&
                pd.UserId == user.Id &&
                pd.OrganizationId == null));

        sutProvider.GetDependency<ILogger<PlayDataService>>()
            .Received(1)
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(user.Id.ToString()) && o.ToString().Contains(playId)),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Record_User_WhenNotInPlay_DoesNotRecordPlayData(
        User user,
        SutProvider<PlayDataService> sutProvider)
    {
        sutProvider.GetDependency<IPlayIdService>()
            .InPlay(out Arg.Any<string>())
            .Returns(x =>
            {
                x[0] = null;
                return false;
            });

        await sutProvider.Sut.Record(user);

        await sutProvider.GetDependency<IPlayDataRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<PlayData>());

        sutProvider.GetDependency<ILogger<PlayDataService>>()
            .DidNotReceive()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Record_Organization_WhenInPlay_RecordsPlayData(
        string playId,
        Organization organization,
        SutProvider<PlayDataService> sutProvider)
    {
        sutProvider.GetDependency<IPlayIdService>()
            .InPlay(out Arg.Any<string>())
            .Returns(x =>
            {
                x[0] = playId;
                return true;
            });

        await sutProvider.Sut.Record(organization);

        await sutProvider.GetDependency<IPlayDataRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<PlayData>(pd =>
                pd.PlayId == playId &&
                pd.OrganizationId == organization.Id &&
                pd.UserId == null));

        sutProvider.GetDependency<ILogger<PlayDataService>>()
            .Received(1)
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(organization.Id.ToString()) && o.ToString().Contains(playId)),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Record_Organization_WhenNotInPlay_DoesNotRecordPlayData(
        Organization organization,
        SutProvider<PlayDataService> sutProvider)
    {
        sutProvider.GetDependency<IPlayIdService>()
            .InPlay(out Arg.Any<string>())
            .Returns(x =>
            {
                x[0] = null;
                return false;
            });

        await sutProvider.Sut.Record(organization);

        await sutProvider.GetDependency<IPlayDataRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<PlayData>());

        sutProvider.GetDependency<ILogger<PlayDataService>>()
            .DidNotReceive()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }
}
