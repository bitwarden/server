using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Commands;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.EmergencyAccess;

[SutProviderCustomize]
public class DeleteEmergencyAccessCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteByIdGrantorIdAsync_EmergencyAccessNotFound_ThrowsBadRequest(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid emergencyAccessId,
        Guid grantorId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdGrantorIdAsync(emergencyAccessId, grantorId)
            .Returns((EmergencyAccessDetails)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteByIdGrantorIdAsync(emergencyAccessId, grantorId));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteByIdGrantorIdAsync_ValidRequest_DeletesAndReturnsDetails(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid emergencyAccessId,
        Guid grantorId,
        Guid granteeId)
    {
        var emergencyAccessDetails = new EmergencyAccessDetails
        {
            Id = emergencyAccessId,
            GrantorId = grantorId,
            GranteeId = granteeId,
            Status = EmergencyAccessStatusType.Confirmed,
            Type = EmergencyAccessType.View
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdGrantorIdAsync(emergencyAccessId, grantorId)
            .Returns(emergencyAccessDetails);

        var result = await sutProvider.Sut.DeleteByIdGrantorIdAsync(emergencyAccessId, grantorId);

        Assert.NotNull(result);
        Assert.Equal(emergencyAccessId, result.Id);
        Assert.Equal(grantorId, result.GrantorId);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<Core.Auth.Entities.EmergencyAccess>(ea => ea.Id == emergencyAccessId));
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAllByGrantorIdAsync_NoEmergencyAccessRecords_ReturnsEmptyCollection(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(grantorId)
            .Returns(new List<EmergencyAccessDetails>());

        var result = await sutProvider.Sut.DeleteAllByGrantorIdAsync(grantorId);

        Assert.NotNull(result);
        Assert.Empty(result);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteAllByGrantorIdAsync_MultipleRecords_DeletesAllAndReturnsDetails(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorId)
    {
        var emergencyAccessDetails1 = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorId,
            GranteeId = Guid.NewGuid(),
            Status = EmergencyAccessStatusType.Confirmed,
            Type = EmergencyAccessType.View
        };

        var emergencyAccessDetails2 = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorId,
            GranteeId = Guid.NewGuid(),
            Status = EmergencyAccessStatusType.Invited,
            Type = EmergencyAccessType.Takeover
        };

        var emergencyAccessDetails3 = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorId,
            GranteeId = Guid.NewGuid(),
            Type = EmergencyAccessType.View
        };

        var allDetails = new List<EmergencyAccessDetails>
        {
            emergencyAccessDetails1,
            emergencyAccessDetails2,
            emergencyAccessDetails3
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(grantorId)
            .Returns(allDetails);

        var result = await sutProvider.Sut.DeleteAllByGrantorIdAsync(grantorId);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(3)
            .DeleteAsync(Arg.Any<Core.Auth.Entities.EmergencyAccess>());
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }

    [Theory, BitAutoData]
    public async Task DeleteAllByGrantorIdAsync_SingleRecord_DeletesAndReturnsDetails(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorId,
        Guid granteeId)
    {
        var emergencyAccessId = Guid.NewGuid();
        var emergencyAccessDetails = new EmergencyAccessDetails
        {
            Id = emergencyAccessId,
            GrantorId = grantorId,
            GranteeId = granteeId,
            Status = EmergencyAccessStatusType.Confirmed,
            Type = EmergencyAccessType.Takeover
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(grantorId)
            .Returns([emergencyAccessDetails]);

        var result = await sutProvider.Sut.DeleteAllByGrantorIdAsync(grantorId);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(emergencyAccessId, result.First().Id);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<Core.Auth.Entities.EmergencyAccess>(ea => ea.Id == emergencyAccessId));
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }
}
