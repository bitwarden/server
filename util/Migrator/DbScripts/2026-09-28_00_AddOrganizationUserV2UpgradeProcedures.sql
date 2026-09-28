CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadManyV2UpgradeDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A row without a user key id cannot be upgraded. The server has nothing to validate the admin's
    -- re-wrapped key against, so the row is not returned.
    SELECT
        OU.[Id] AS [OrganizationUserId],
        U.[UserKeyId],
        OU.[ResetPasswordKey] AS [AccountRecoveryKey],
        OU.[V2UpgradeToken]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[V2UpgradeToken] IS NOT NULL
        AND OU.[ResetPasswordKey] IS NOT NULL
        AND U.[UserKeyId] IS NOT NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateManyV2UpgradedAccountRecoveryKeys]
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationUserJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationUserInput AS TABLE (
        [Id] UNIQUEIDENTIFIER,
        [UserKeyId] VARCHAR(32),
        [AccountRecoveryKey] VARCHAR(MAX)
    )

    DECLARE @UpdatedUserIds [dbo].[GuidIdArray]

    INSERT INTO @OrganizationUserInput
    SELECT
        [Id],
        [UserKeyId],
        [AccountRecoveryKey]
    FROM OPENJSON(@OrganizationUserJson)
    WITH (
        [Id] UNIQUEIDENTIFIER '$.OrganizationUserId',
        [UserKeyId] VARCHAR(32) '$.UserKeyId',
        [AccountRecoveryKey] VARCHAR(MAX) '$.AccountRecoveryKey'
    )

    -- The key id is checked in the join, so a rotation cannot slip in between the check and the write. A row
    -- that no longer matches is skipped and stays pending. A NULL key unenrolls the member.
    UPDATE
        [dbo].[OrganizationUser]
    SET
        [ResetPasswordKey] = OUI.[AccountRecoveryKey],
        [V2UpgradeToken] = NULL
    OUTPUT
        INSERTED.[UserId] INTO @UpdatedUserIds
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserInput OUI ON OU.[Id] = OUI.[Id]
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId] AND U.[UserKeyId] = OUI.[UserKeyId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[V2UpgradeToken] IS NOT NULL

    -- Bump the account revision date of the members whose row was updated.
    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UpdatedUserIds
END
GO
