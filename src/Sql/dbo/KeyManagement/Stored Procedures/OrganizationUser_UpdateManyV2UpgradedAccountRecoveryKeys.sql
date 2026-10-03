CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManyV2UpgradedAccountRecoveryKeys]
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationUserJson NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationUserInput AS TABLE (
        [Id] UNIQUEIDENTIFIER,
        [UserKeyId] VARCHAR(32),
        [AccountRecoveryKey] VARCHAR(MAX)
    )

    DECLARE @Updated AS TABLE (
        [OrganizationUserId] UNIQUEIDENTIFIER,
        [UserId] UNIQUEIDENTIFIER
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

    -- The key id and the enrollment are checked in the statement, so a rotation or a withdrawal cannot slip in
    -- between the check and the write. A row that no longer matches is skipped. A NULL key unenrolls the member.
    UPDATE
        [dbo].[OrganizationUser]
    SET
        [ResetPasswordKey] = OUI.[AccountRecoveryKey],
        [V2UpgradeToken] = NULL,
        [RevisionDate] = @RevisionDate
    OUTPUT
        INSERTED.[Id],
        INSERTED.[UserId]
    INTO @Updated
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserInput OUI ON OU.[Id] = OUI.[Id]
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId] AND U.[UserKeyId] = OUI.[UserKeyId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[V2UpgradeToken] IS NOT NULL
        AND OU.[ResetPasswordKey] IS NOT NULL

    -- Bump the account revision date of the members whose row was updated.
    INSERT INTO @UpdatedUserIds ([Id])
    SELECT [UserId] FROM @Updated

    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UpdatedUserIds

    SELECT [OrganizationUserId] FROM @Updated
END
