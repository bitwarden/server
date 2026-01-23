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
    /// <summary>
    /// Verifies that attempting to delete a non-existent emergency access record
    /// throws a <see cref="BadRequestException"/> and does not call delete or send email.
    /// </summary>
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

    /// <summary>
    /// Verifies that attempting to delete a non-existent emergency access record
    /// throws a <see cref="BadRequestException"/> and does not call delete or send email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteByIdGrantorIdAsync_DeletesEmergencyAccessAndSendsEmail(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdGrantorIdAsync(emergencyAccessDetails.Id, emergencyAccessDetails.GrantorId)
            .Returns(emergencyAccessDetails);

        var result = await sutProvider.Sut.DeleteByIdGrantorIdAsync(emergencyAccessDetails.Id, emergencyAccessDetails.GrantorId);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Any<ICollection<Guid>>());
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }

    /// <summary>
    /// Verifies that when a grantor has no emergency access records, the method returns
    /// an empty collection and does not attempt to delete or send email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByGrantorIdAsync_NoEmergencyAccessRecords_ReturnsEmptyCollection(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(grantorId)
            .Returns([]);

        var result = await sutProvider.Sut.DeleteAllByGrantorIdAsync(grantorId);

        Assert.NotNull(result);
        Assert.Empty(result);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    /// <summary>
    /// Verifies that when a grantor has multiple emergency access records, all records are deleted,
    /// the details are returned, and a single notification email is sent to the grantor.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByGrantorIdAsync_MultipleRecords_DeletesAllReturnsDetailsSendsSingleEmail(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails1,
        EmergencyAccessDetails emergencyAccessDetails2,
        EmergencyAccessDetails emergencyAccessDetails3)
    {
        // Arrange
        // link all details to the same grantor
        emergencyAccessDetails2.GrantorId = emergencyAccessDetails1.GrantorId;
        emergencyAccessDetails2.GrantorEmail = emergencyAccessDetails1.GrantorEmail;
        emergencyAccessDetails3.GrantorId = emergencyAccessDetails1.GrantorId;
        emergencyAccessDetails3.GrantorEmail = emergencyAccessDetails1.GrantorEmail;

        var allDetails = new List<EmergencyAccessDetails>
        {
            emergencyAccessDetails1,
            emergencyAccessDetails2,
            emergencyAccessDetails3
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(emergencyAccessDetails1.GrantorId)
            .Returns(allDetails);

        // Act
        var result = await sutProvider.Sut.DeleteAllByGrantorIdAsync(emergencyAccessDetails1.GrantorId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Any<ICollection<Guid>>());
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }

    /// <summary>
    /// Verifies that when a grantor has a single emergency access record, it is deleted,
    /// the details are returned, and a notification email is sent.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByGrantorIdAsync_SingleRecord_DeletesAndReturnsDetailsSendsSingleEmail(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails,
        Guid grantorId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(grantorId)
            .Returns([emergencyAccessDetails]);

        var result = await sutProvider.Sut.DeleteAllByGrantorIdAsync(grantorId);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(emergencyAccessDetails.Id, result.First().Id);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Any<ICollection<Guid>>());
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }

    /// <summary>
    /// Verifies that when a grantee has no emergency access records, the method returns
    /// an empty collection and does not attempt to delete or send email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByGranteeIdAsync_NoEmergencyAccessRecords_ReturnsEmptyCollection(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid granteeId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGranteeIdAsync(granteeId)
            .Returns([]);

        var result = await sutProvider.Sut.DeleteAllByGranteeIdAsync(granteeId);

        Assert.NotNull(result);
        Assert.Empty(result);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    /// <summary>
    /// Verifies that when a grantee has a single emergency access record, it is deleted,
    /// the details are returned, and a notification email is sent to the grantor.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByGranteeIdAsync_SingleRecord_DeletesAndReturnsDetailsSendsSingleEmail(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails,
        Guid granteeId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGranteeIdAsync(granteeId)
            .Returns([emergencyAccessDetails]);

        var result = await sutProvider.Sut.DeleteAllByGranteeIdAsync(granteeId);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(emergencyAccessDetails.Id, result.First().Id);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Any<ICollection<Guid>>());
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }

    /// <summary>
    /// Verifies that when a grantee has multiple emergency access records from different grantors,
    /// all records are deleted, the details are returned, and a single notification email is sent
    /// to all affected grantors individually.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByGranteeIdAsync_MultipleRecords_DeletesAllReturnsDetailsSendsMultipleEmails(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails1,
        EmergencyAccessDetails emergencyAccessDetails2,
        EmergencyAccessDetails emergencyAccessDetails3)
    {
        // link all details to the same grantee
        emergencyAccessDetails2.GranteeId = emergencyAccessDetails1.GranteeId;
        emergencyAccessDetails2.GranteeEmail = emergencyAccessDetails1.GranteeEmail;
        emergencyAccessDetails3.GranteeId = emergencyAccessDetails1.GranteeId;
        emergencyAccessDetails3.GranteeEmail = emergencyAccessDetails1.GranteeEmail;

        var allDetails = new List<EmergencyAccessDetails>
        {
            emergencyAccessDetails1,
            emergencyAccessDetails2,
            emergencyAccessDetails3
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGranteeIdAsync((Guid)emergencyAccessDetails1.GranteeId)
            .Returns(allDetails);

        var result = await sutProvider.Sut.DeleteAllByGranteeIdAsync((Guid)emergencyAccessDetails1.GranteeId);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Any<ICollection<Guid>>());
        await sutProvider.GetDependency<IMailer>()
            .Received(allDetails.Count)
            .SendEmail(Arg.Any<EmergencyAccessRemoveGranteesMail>());
    }
}
