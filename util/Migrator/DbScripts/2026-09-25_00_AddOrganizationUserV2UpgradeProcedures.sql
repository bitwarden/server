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

    DECLARE @ExpectedCount INT = (SELECT COUNT(1) FROM @OrganizationUserInput)

    BEGIN TRANSACTION

    -- The join on [UserKeyId] rejects a key re-wrapped against a user key that has been rotated again since.
    -- The same statement clears the token, so an upgrade cannot be replayed.
    -- A NULL [AccountRecoveryKey] is not a missing value. It unenrolls the member from account recovery.
    UPDATE
        [dbo].[OrganizationUser]
    SET
        [ResetPasswordKey] = OUI.[AccountRecoveryKey],
        [V2UpgradeToken] = NULL
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserInput OUI ON OU.[Id] = OUI.[Id]
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId] AND U.[UserKeyId] = OUI.[UserKeyId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[V2UpgradeToken] IS NOT NULL

    DECLARE @UpdatedCount INT = @@ROWCOUNT

    IF @UpdatedCount <> @ExpectedCount
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0
        RETURN
    END

    COMMIT TRANSACTION

    SELECT @UpdatedCount
END
GO
