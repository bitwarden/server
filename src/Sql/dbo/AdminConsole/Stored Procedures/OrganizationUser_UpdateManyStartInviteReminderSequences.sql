CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManyStartInviteReminderSequences]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @DueDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Does not bump the account revision date: the reminder columns are not synced to clients
    UPDATE
        OU
    SET
        OU.[InviteRemindersSentCount] = 0,
        -- Keep a later stored due date
        OU.[InviteReminderDueDate] = CASE
            WHEN OU.[InviteReminderDueDate] > @DueDate
            THEN OU.[InviteReminderDueDate]
            ELSE @DueDate
        END
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
END
