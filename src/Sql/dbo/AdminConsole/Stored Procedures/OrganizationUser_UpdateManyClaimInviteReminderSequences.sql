CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManyClaimInviteReminderSequences]
    @Now DATETIME2(7),
    @OldestDueDate DATETIME2(7),
    @NextDueDate DATETIME2(7),
    @BatchSize INT
AS
BEGIN
    SET NOCOUNT ON

    -- Status and the reminder limit are literal values. With parameters, SQL Server cannot use
    -- the filtered index IX_OrganizationUser_InviteReminders_Filtered.
    ;WITH [Due] AS (
        SELECT TOP (@BatchSize)
            OU.[Id],
            OU.[OrganizationId],
            OU.[Email],
            OU.[InviteReminderDueDate],
            OU.[InviteRemindersSentCount]
        FROM
            [dbo].[OrganizationUser] OU WITH (UPDLOCK, READPAST)
        INNER JOIN
            [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
        WHERE
            OU.[Status] = 0 -- 0 = Invited
            AND OU.[Email] IS NOT NULL
            AND OU.[InviteReminderDueDate] IS NOT NULL
            AND OU.[InviteReminderDueDate] <= @Now
            AND OU.[InviteReminderDueDate] >= @OldestDueDate
            AND OU.[InviteRemindersSentCount] < 3
            AND O.[Enabled] = 1
        ORDER BY
            OU.[InviteReminderDueDate] ASC
    )
    UPDATE
        [Due]
    SET
        [InviteRemindersSentCount] = [InviteRemindersSentCount] + 1,
        [InviteReminderDueDate] = @NextDueDate
    OUTPUT
        inserted.[Id],
        inserted.[OrganizationId],
        inserted.[Email],
        inserted.[InviteRemindersSentCount]
END
