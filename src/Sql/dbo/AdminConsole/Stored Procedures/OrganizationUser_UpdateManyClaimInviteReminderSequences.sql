CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManyClaimInviteReminderSequences]
    @Now DATETIME2(7),
    @OldestDueDate DATETIME2(7),
    @NextDueDate DATETIME2(7),
    @BatchSize INT
AS
BEGIN
    SET NOCOUNT ON

    -- Status is a literal value. With a parameter, SQL Server cannot use
    -- the filtered index IX_OrganizationUser_InviteReminders_Filtered.
    -- The last reminder clears the due date, which removes the member from the index.
    -- The reminder limit 3 is in the WHERE and the SET. Change both together.
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
        [InviteReminderDueDate] = CASE
            WHEN [InviteRemindersSentCount] + 1 >= 3
            THEN NULL
            ELSE @NextDueDate
        END
    OUTPUT
        inserted.[Id],
        inserted.[OrganizationId],
        inserted.[Email],
        inserted.[InviteRemindersSentCount]
END
