using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Commands;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
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
    public async Task DeleteByIdAndUserIdAsync_EmergencyAccessNotFound_ThrowsBadRequestAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid emergencyAccessId,
        Guid userId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdAsync(emergencyAccessId)
            .Returns((EmergencyAccessDetails?)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteByIdAndUserIdAsync(emergencyAccessId, userId));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    /// <summary>
    /// Verifies that an emergency access record is deleted by ID and user ID,
    /// and that a notification email is sent to the grantor.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteByIdAndUserIdAsync_DeletesEmergencyAccessAndSendsEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdAsync(emergencyAccessDetails.Id)
            .Returns(emergencyAccessDetails);

        await sutProvider.Sut.DeleteByIdAndUserIdAsync(emergencyAccessDetails.Id, emergencyAccessDetails.GrantorId);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteAsync(emergencyAccessDetails);
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(emergencyAccessDetails.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains(emergencyAccessDetails.GranteeEmail)));
    }

    /// <summary>
    /// Verifies that when the grantor email is null, the record is deleted
    /// but no email notification is sent.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteByIdAndUserIdAsync_NullGrantorEmail_DeletesButDoesNotSendEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails)
    {
        emergencyAccessDetails.GrantorEmail = null;

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdAsync(emergencyAccessDetails.Id)
            .Returns(emergencyAccessDetails);

        await sutProvider.Sut.DeleteByIdAndUserIdAsync(emergencyAccessDetails.Id, emergencyAccessDetails.GrantorId);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteAsync(emergencyAccessDetails);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
        sutProvider.GetDependency<ILogger<DeleteEmergencyAccessCommand>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(emergencyAccessDetails.GrantorId.ToString())
                    && o.ToString().Contains("GrantorEmail missing: True")
                    && o.ToString().Contains("GranteeEmail missing: False")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Verifies that when the grantee email is null, the record is deleted
    /// but no email notification is sent.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteByIdAndUserIdAsync_NullGranteeEmail_DeletesButDoesNotSendEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails)
    {
        emergencyAccessDetails.GranteeEmail = null;

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdAsync(emergencyAccessDetails.Id)
            .Returns(emergencyAccessDetails);

        await sutProvider.Sut.DeleteByIdAndUserIdAsync(emergencyAccessDetails.Id, emergencyAccessDetails.GrantorId);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteAsync(emergencyAccessDetails);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
        sutProvider.GetDependency<ILogger<DeleteEmergencyAccessCommand>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(emergencyAccessDetails.GrantorId.ToString())
                    && o.ToString().Contains("GrantorEmail missing: False")
                    && o.ToString().Contains("GranteeEmail missing: True")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Verifies that a grantee (not just a grantor) can delete an emergency access record,
    /// and that the grantor still receives a notification email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteByIdAndUserIdAsync_GranteeDeletes_DeletesAndSendsEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdAsync(emergencyAccessDetails.Id)
            .Returns(emergencyAccessDetails);

        // Act as the grantee, not the grantor
        await sutProvider.Sut.DeleteByIdAndUserIdAsync(emergencyAccessDetails.Id, emergencyAccessDetails.GranteeId.Value);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteAsync(emergencyAccessDetails);
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(emergencyAccessDetails.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains(emergencyAccessDetails.GranteeEmail)));
    }

    /// <summary>
    /// Verifies that a user who is neither the grantor nor the grantee cannot delete
    /// the emergency access record and receives a <see cref="BadRequestException"/>.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteByIdAndUserIdAsync_UnauthorizedUser_ThrowsBadRequestAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails,
        Guid unauthorizedUserId)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetDetailsByIdAsync(emergencyAccessDetails.Id)
            .Returns(emergencyAccessDetails);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteByIdAndUserIdAsync(emergencyAccessDetails.Id, unauthorizedUserId));

        Assert.Contains("Emergency Access not valid.", exception.Message);
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    /// <summary>
    /// Verifies that <see cref="IDeleteEmergencyAccessCommand.DeleteAllByUserIdAsync"/> correctly
    /// delegates to <see cref="IDeleteEmergencyAccessCommand.DeleteAllByUserIdsAsync"/>
    /// using a single-element collection containing the provided user ID.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdAsync_DelegatesToDeleteAllByUserIdsAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails emergencyAccessDetails,
        Guid userId)
    {
        emergencyAccessDetails.GranteeId = userId;

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(userId)))
            .Returns([emergencyAccessDetails]);

        await sutProvider.Sut.DeleteAllByUserIdAsync(userId);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 1 && ids.Contains(userId)));
        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 1 && ids.Contains(emergencyAccessDetails.Id)));
    }

    /// <summary>
    /// Verifies that passing an empty list of user IDs does not attempt to delete or send email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_EmptyList_DoesNotDeleteOrSendEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Any<ICollection<Guid>>())
            .Returns([]);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    /// <summary>
    /// Verifies that when user IDs don't match any emergency access records,
    /// the method does not attempt to delete or send email.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_NoRecordsFound_DoesNotDeleteOrSendEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        List<Guid> userIds)
    {
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(userIds)
            .Returns([]);

        await sutProvider.Sut.DeleteAllByUserIdsAsync(userIds);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
    }

    /// <summary>
    /// Verifies that when a single user ID is a grantee with multiple grantors,
    /// all records are deleted and each grantor receives one email notification.
    ///
    /// Scenario: Alice is a grantee with emergency access TO Bob's, Carol's, and David's vaults.
    /// When Alice is removed, Bob, Carol, and David each receive an email notification.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_SingleUserIdAsGranteeOnly_NotifiesGrantorsAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails bobAliceRecord,
        EmergencyAccessDetails carolAliceRecord,
        EmergencyAccessDetails davidAliceRecord,
        Guid granteeUserIdAlice)
    {
        // Alice (grantee) has emergency access to Bob's, Carol's, and David's vaults
        bobAliceRecord.GranteeId = granteeUserIdAlice; // Bob granted Alice access to Bob's vault
        carolAliceRecord.GranteeId = granteeUserIdAlice; // Carol granted Alice access to Carol's vault
        davidAliceRecord.GranteeId = granteeUserIdAlice; // David granted Alice access to David's vault

        var allDetails = new List<EmergencyAccessDetails>
        {
            bobAliceRecord,
            carolAliceRecord,
            davidAliceRecord
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(granteeUserIdAlice)))
            .Returns(allDetails);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([granteeUserIdAlice]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 3 &&
                ids.Contains(bobAliceRecord.Id) &&
                ids.Contains(carolAliceRecord.Id) &&
                ids.Contains(davidAliceRecord.Id)));
        // Each grantor gets one email
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(bobAliceRecord.GrantorEmail)));
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(carolAliceRecord.GrantorEmail)));
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(davidAliceRecord.GrantorEmail)));
    }

    /// <summary>
    /// Verifies that when a single user ID is a grantor with multiple grantees,
    /// all records are deleted and the grantor is notified about their grantees being removed.
    ///
    /// Scenario: Bob is a grantor who has given Alice and Carol emergency access to his vault.
    /// When Bob is removed, Bob receives ONE email listing both Alice and Carol.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_SingleUserIdAsGrantorOnly_NotifiesGrantorAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails bobAliceRecord,
        EmergencyAccessDetails bobCarolRecord,
        Guid grantorUserIdBob)
    {
        // Bob (grantor) has given Alice and Carol emergency access to his vault
        bobAliceRecord.GrantorId = grantorUserIdBob; // Bob granted Alice access to his vault
        bobCarolRecord.GrantorId = grantorUserIdBob; // Bob granted Carol access to his vault
        bobAliceRecord.GrantorEmail = "bob@example.com";
        bobCarolRecord.GrantorEmail = "bob@example.com";

        var allDetails = new List<EmergencyAccessDetails>
        {
            bobAliceRecord,
            bobCarolRecord
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(grantorUserIdBob)))
            .Returns(allDetails);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([grantorUserIdBob]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(bobAliceRecord.Id) &&
                ids.Contains(bobCarolRecord.Id)));
        // Grantor receives one email listing both their grantees being removed
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(bobAliceRecord.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains(bobAliceRecord.GranteeEmail) &&
                mail.View.RemovedGranteeEmails.Contains(bobCarolRecord.GranteeEmail)));
    }

    /// <summary>
    /// Verifies that when a user ID is both a grantor and a grantee,
    /// all affected grantors are notified: the user's grantors (for grantee role)
    /// AND the user themselves (for grantor role with their grantees).
    ///
    /// Scenario: Bob plays both roles:
    /// - As GRANTEE: Bob has emergency access to Alice's and Carol's vaults
    /// - As GRANTOR: Bob has given David and Emma emergency access to his vault
    /// When Bob is removed, THREE emails are sent:
    /// 1. Alice receives email: "Bob removed"
    /// 2. Carol receives email: "Bob removed"
    /// 3. Bob receives email: "David and Emma removed" (notified about his own grantees)
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_SingleUserIdBothRoles_NotifiesAllGrantorsAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails aliceBobRecord,
        EmergencyAccessDetails carolBobRecord,
        EmergencyAccessDetails bobDavidRecord,
        EmergencyAccessDetails bobEmmaRecord,
        Guid userIdBob)
    {
        // Bob as GRANTEE: has emergency access to Alice's and Carol's vaults
        aliceBobRecord.GranteeId = userIdBob; // Alice granted Bob access to Alice's vault
        aliceBobRecord.GranteeEmail = "bob@example.com";
        carolBobRecord.GranteeId = userIdBob; // Carol granted Bob access to Carol's vault
        carolBobRecord.GranteeEmail = "bob@example.com";

        // Bob as GRANTOR: has given David and Emma emergency access to his vault
        bobDavidRecord.GrantorId = userIdBob; // Bob granted David access to his vault
        bobDavidRecord.GrantorEmail = "bob@example.com";
        bobEmmaRecord.GrantorId = userIdBob; // Bob granted Emma access to his vault
        bobEmmaRecord.GrantorEmail = "bob@example.com";

        var allDetails = new List<EmergencyAccessDetails>
        {
            aliceBobRecord,
            carolBobRecord,
            bobDavidRecord,
            bobEmmaRecord
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(userIdBob)))
            .Returns(allDetails);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([userIdBob]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 4 &&
                ids.Contains(aliceBobRecord.Id) &&
                ids.Contains(carolBobRecord.Id) &&
                ids.Contains(bobDavidRecord.Id) &&
                ids.Contains(bobEmmaRecord.Id)));

        // Email 1: Alice receives "Bob removed" (Bob was grantee to Alice's vault)
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(aliceBobRecord.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains("bob@example.com")));

        // Email 2: Carol receives "Bob removed" (Bob was grantee to Carol's vault)
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(carolBobRecord.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains("bob@example.com")));

        // Email 3: Bob receives "David and Emma removed" (Bob was grantor, his grantees removed)
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains("bob@example.com") &&
                mail.View.RemovedGranteeEmails.Contains(bobDavidRecord.GranteeEmail) &&
                mail.View.RemovedGranteeEmails.Contains(bobEmmaRecord.GranteeEmail)));
    }

    /// <summary>
    /// Verifies that multiple user IDs as grantees are properly deleted
    /// and their respective grantors are notified.
    ///
    /// Scenario: Alice and Bob are both grantees (to different grantors' vaults).
    /// - Alice has emergency access to Carol's vault
    /// - Bob has emergency access to David's vault
    /// When Alice and Bob are removed, Carol and David each receive separate email notifications.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_MultipleUserIdsAllGrantees_SendsMultipleEmailsAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails carolAliceRecord,
        EmergencyAccessDetails davidBobRecord,
        Guid granteeUserIdAlice,
        Guid granteeUserIdBob)
    {
        carolAliceRecord.GranteeId = granteeUserIdAlice; // Carol granted Alice access to Carol's vault
        davidBobRecord.GranteeId = granteeUserIdBob; // David granted Bob access to David's vault

        var allDetails = new List<EmergencyAccessDetails>
        {
            carolAliceRecord,
            davidBobRecord
        };

        var userIds = new List<Guid> { granteeUserIdAlice, granteeUserIdBob };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(userIds)
            .Returns(allDetails);

        await sutProvider.Sut.DeleteAllByUserIdsAsync(userIds);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(carolAliceRecord.Id) &&
                ids.Contains(davidBobRecord.Id)));
        // Carol gets email about Alice being removed
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(carolAliceRecord.GrantorEmail)));
        // David gets email about Bob being removed
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(davidBobRecord.GrantorEmail)));
    }

    /// <summary>
    /// Verifies that multiple user IDs as grantors are properly deleted
    /// and each grantor is notified about their grantees being removed.
    ///
    /// Scenario: Bob and Carol are both grantors (vault owners with grantees).
    /// - Bob has given Alice emergency access to his vault
    /// - Carol has given David emergency access to her vault
    /// When Bob and Carol are removed, they each receive separate email notifications:
    /// - Bob receives email: "Alice removed"
    /// - Carol receives email: "David removed"
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_MultipleUserIdsAllGrantors_NotifiesEachGrantorAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        EmergencyAccessDetails bobAliceRecord,
        EmergencyAccessDetails carolDavidRecord,
        Guid grantorUserIdBob,
        Guid grantorUserIdCarol)
    {
        bobAliceRecord.GrantorId = grantorUserIdBob; // Bob (grantor) has given Alice emergency access
        carolDavidRecord.GrantorId = grantorUserIdCarol; // Carol (grantor) has given David emergency access

        var allDetails = new List<EmergencyAccessDetails>
        {
            bobAliceRecord,
            carolDavidRecord
        };

        var userIds = new List<Guid> { grantorUserIdBob, grantorUserIdCarol };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(userIds)
            .Returns(allDetails);

        await sutProvider.Sut.DeleteAllByUserIdsAsync(userIds);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(bobAliceRecord.Id) &&
                ids.Contains(carolDavidRecord.Id)));

        // Bob gets email about Alice being removed
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(bobAliceRecord.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains(bobAliceRecord.GranteeEmail)));

        // Carol gets email about David being removed
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(carolDavidRecord.GrantorEmail) &&
                mail.View.RemovedGranteeEmails.Contains(carolDavidRecord.GranteeEmail)));
    }

    /// <summary>
    /// Verifies that when multiple grantees share overlapping grantors,
    /// each grantor receives exactly one email with only their specific removed grantees.
    ///
    /// Scenario: Ali and Bob are grantees being removed, with overlapping grantors:
    /// - Cara granted Ali emergency access to her vault
    /// - Dave granted Ali and Bob emergency access to his vault
    /// - Eve granted Bob emergency access to her vault
    /// Expected email notifications:
    /// - Cara receives email: "Ali removed" (only Ali, not Bob)
    /// - Dave receives email: "Ali and Bob removed" (both, since Dave is shared)
    /// - Eve receives email: "Bob removed" (only Bob, not Ali)
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_MultipleUsersOverlappingGrantors_EachGrantorGetsCorrectSubsetAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid granteeUserIdAli,
        Guid granteeUserIdBob,
        string grantorEmailCara,
        string grantorEmailDave,
        string grantorEmailEve,
        string granteeEmailAli,
        string granteeEmailBob)
    {
        // GrantorId is not set on these records as the command only uses GrantorEmail for
        // grouping and notification — GrantorId plays no role in the logic under test.

        // Cara (grantor) granted Ali emergency access to her vault
        var caraAliRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeUserIdAli,
            GranteeEmail = granteeEmailAli,
            GrantorEmail = grantorEmailCara
        };

        // Dave (grantor) granted Ali emergency access to his vault
        var daveAliRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeUserIdAli,
            GranteeEmail = granteeEmailAli,
            GrantorEmail = grantorEmailDave // Dave also granted Bob access
        };

        // Dave (grantor) granted Bob emergency access to his vault
        var daveBobRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeUserIdBob,
            GranteeEmail = granteeEmailBob,
            GrantorEmail = grantorEmailDave // Dave also granted Ali access
        };

        // Eve (grantor) granted Bob emergency access to her vault
        var eveBobRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeUserIdBob,
            GranteeEmail = granteeEmailBob,
            GrantorEmail = grantorEmailEve
        };

        var allDetails = new List<EmergencyAccessDetails> { caraAliRecord, daveAliRecord, daveBobRecord, eveBobRecord };
        var userIdsToDelete = new List<Guid> { granteeUserIdAli, granteeUserIdBob };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(userIdsToDelete)
            .Returns(allDetails);

        await sutProvider.Sut.DeleteAllByUserIdsAsync(userIdsToDelete);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 4 &&
                ids.Contains(caraAliRecord.Id) &&
                ids.Contains(daveAliRecord.Id) &&
                ids.Contains(daveBobRecord.Id) &&
                ids.Contains(eveBobRecord.Id)));
        // Cara gets email with only Ali
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(grantorEmailCara) &&
                mail.View.RemovedGranteeEmails.Contains(granteeEmailAli)));
        // Dave gets email with both Ali and Bob (shared grantor)
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(grantorEmailDave) &&
                mail.View.RemovedGranteeEmails.Contains(granteeEmailAli) &&
                mail.View.RemovedGranteeEmails.Contains(granteeEmailBob)));
        // Eve gets email with only Bob
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(grantorEmailEve) &&
                mail.View.RemovedGranteeEmails.Contains(granteeEmailBob)));
    }

    /// <summary>
    /// Verifies that records with null grantee emails are handled gracefully
    /// and don't cause errors during email notification processing.
    ///
    /// Scenario: Bob granted Alice emergency access to his vault. Alice accepted, so EA.Email
    /// was cleared and only her user account held her email. Alice's account was later deleted,
    /// leaving both GranteeU.Email (LEFT JOIN miss) and EA.Email null — so GranteeEmail is null.
    /// The record is deleted but no email is sent because there's no valid grantee email to include.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_NullGranteeEmail_HandledGracefullyAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid granteeUserIdAlice,
        string grantorEmailBob)
    {
        // Alice accepted EA (EA.Email cleared), then her account was deleted (LEFT JOIN miss) — GranteeEmail is null
        var bobAliceRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeUserIdAlice, // Alice's user ID (account since deleted)
            GranteeEmail = null, // Null: EA.Email was cleared on accept, user account no longer exists
            GrantorEmail = grantorEmailBob // Bob's email
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(granteeUserIdAlice)))
            .Returns([bobAliceRecord]);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([granteeUserIdAlice]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 1 && ids.Contains(bobAliceRecord.Id)));
        // Email should not be sent if grantee email is null
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
        sutProvider.GetDependency<ILogger<DeleteEmergencyAccessCommand>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(granteeUserIdAlice.ToString())
                    && o.ToString().Contains("missing GranteeEmail")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Verifies that records with null grantor emails are filtered out
    /// and don't cause errors during email notification processing.
    ///
    /// Scenario: Bob granted Alice emergency access to his vault, then Bob's account was deleted.
    /// Unlike GranteeEmail (which falls back to EA.Email), GrantorEmail has no fallback — it comes
    /// entirely from the LEFT JOIN on the User table. When the grantor's account is deleted,
    /// the LEFT JOIN misses and GrantorEmail is null. The record is deleted but no email is sent.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_NullGrantorEmail_DeletesButDoesNotSendEmailAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorUserIdBob,
        string granteeEmailAlice)
    {
        // Bob's account was deleted — LEFT JOIN misses, no fallback column exists for GrantorEmail
        var bobAliceRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorUserIdBob, // Bob's user ID (account since deleted)
            GrantorEmail = null, // Null: no EA.Email fallback exists for grantors, account no longer exists
            GranteeEmail = granteeEmailAlice // Alice's email
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(grantorUserIdBob)))
            .Returns([bobAliceRecord]);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([grantorUserIdBob]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 1 && ids.Contains(bobAliceRecord.Id)));
        // Email should not be sent if grantor email is null
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<EmergencyAccessRemoveGranteesMailView>(default);
        sutProvider.GetDependency<ILogger<DeleteEmergencyAccessCommand>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(grantorUserIdBob.ToString())
                    && o.ToString().Contains("missing GrantorEmail")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Verifies that duplicate grantee emails for the same grantor are deduplicated
    /// so the grantor receives exactly one email listing each grantee address only once.
    ///
    /// Scenario: Bob has two EA records pointing to the same grantee email (e.g., from
    /// a re-invite edge case where the prior record wasn't cleaned up). When Bob is removed,
    /// he receives ONE email listing the grantee's email only once — not duplicated.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_DuplicateGranteeEmails_DeduplicatesEmailsInNotificationAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorUserIdBob,
        string granteeEmailAlice)
    {
        const string grantorEmailBob = "bob@example.com";

        // Two records sharing the same grantor and grantee email — grantee email should be deduplicated
        var bobAliceRecord1 = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorUserIdBob,
            GrantorEmail = grantorEmailBob,
            GranteeEmail = granteeEmailAlice
        };
        var bobAliceRecord2 = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorUserIdBob,
            GrantorEmail = grantorEmailBob,
            GranteeEmail = granteeEmailAlice // Same grantee email as record 1
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(grantorUserIdBob)))
            .Returns([bobAliceRecord1, bobAliceRecord2]);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([grantorUserIdBob]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(bobAliceRecord1.Id) &&
                ids.Contains(bobAliceRecord2.Id)));
        // Bob receives one email with the grantee email appearing exactly once
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(grantorEmailBob) &&
                mail.View.RemovedGranteeEmails.Count() == 1 &&
                mail.View.RemovedGranteeEmails.Contains(granteeEmailAlice)));
    }

    /// <summary>
    /// Verifies that when a grantor has multiple grantees and only some have null emails,
    /// the grantor still receives an email listing only the non-null grantee emails.
    ///
    /// Scenario: Bob granted Alice and Carol emergency access to his vault.
    /// Carol's account was later deleted, leaving her GranteeEmail null.
    /// When Bob is removed, he receives ONE email listing only Alice — Carol is excluded
    /// because there is no valid email address to include.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAllByUserIdsAsync_PartialNullGranteeEmails_SendsEmailForNonNullGranteesOnlyAsync(
        SutProvider<DeleteEmergencyAccessCommand> sutProvider,
        Guid grantorUserIdBob,
        Guid granteeUserIdCarol,
        string granteeEmailAlice)
    {
        const string grantorEmailBob = "bob@example.com";

        // Alice's record: valid grantee email
        var bobAliceRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorUserIdBob,
            GrantorEmail = grantorEmailBob,
            GranteeEmail = granteeEmailAlice
        };

        // Carol's record: null grantee email (her account was deleted)
        var bobCarolRecord = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GrantorId = grantorUserIdBob,
            GrantorEmail = grantorEmailBob,
            GranteeId = granteeUserIdCarol, // Carol's user ID (account since deleted)
            GranteeEmail = null
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByUserIdsAsync(Arg.Is<ICollection<Guid>>(ids => ids.Contains(grantorUserIdBob)))
            .Returns([bobAliceRecord, bobCarolRecord]);

        await sutProvider.Sut.DeleteAllByUserIdsAsync([grantorUserIdBob]);

        await sutProvider.GetDependency<IEmergencyAccessRepository>()
            .Received(1)
            .DeleteManyAsync(Arg.Is<ICollection<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(bobAliceRecord.Id) &&
                ids.Contains(bobCarolRecord.Id)));
        // Bob receives one email listing only Alice (Carol excluded — null grantee email)
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<EmergencyAccessRemoveGranteesMail>(mail =>
                mail.ToEmails.Contains(grantorEmailBob) &&
                mail.View.RemovedGranteeEmails.Count() == 1 &&
                mail.View.RemovedGranteeEmails.Contains(granteeEmailAlice)));
        // Carol's record (null grantee email) should trigger a warning with her user ID
        sutProvider.GetDependency<ILogger<DeleteEmergencyAccessCommand>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains(granteeUserIdCarol.ToString())
                    && o.ToString().Contains("missing GranteeEmail")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
    }
}
