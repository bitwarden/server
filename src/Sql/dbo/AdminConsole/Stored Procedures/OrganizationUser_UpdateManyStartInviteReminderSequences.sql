CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManyStartInviteReminderSequences]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @DueDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Do not change the account revision date. Clients do not sync the reminder columns.
    UPDATE
        OU
    SET
        OU.[InviteRemindersSentCount] = 0,
        -- Keep the stored due date if it is later
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
