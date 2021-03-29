-- Check email column is 50 characters long (100 bytes)
IF COL_LENGTH('[dbo].[EmergencyAccess]', 'Email') = 100
BEGIN
	ALTER TABLE [dbo].[EmergencyAccess] 
	ALTER COLUMN 
		Email NVARCHAR(256) NULL
END
GO

-- Recreate procedure EmergencyAccess_Create
IF OBJECT_ID('[dbo].[EmergencyAccess_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_Create]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_Create]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @KeyEncrypted VARCHAR(MAX),
    @Type TINYINT,
    @Status TINYINT,
    @WaitTimeDays SMALLINT,
    @RecoveryInitiatedDate DATETIME2(7),
    @LastNotificationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[EmergencyAccess]
    (
        [Id],
        [GrantorId],
        [GranteeId],
        [Email],
        [KeyEncrypted],
        [Type],
        [Status],
        [WaitTimeDays],
        [RecoveryInitiatedDate],
        [LastNotificationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @GrantorId,
        @GranteeId,
        @Email,
        @KeyEncrypted,
        @Type,
        @Status,
        @WaitTimeDays,
        @RecoveryInitiatedDate,
        @LastNotificationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO

-- Recreate procedure EmergencyAccess_Update
IF OBJECT_ID('[dbo].[EmergencyAccess_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_Update]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_Update]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @KeyEncrypted VARCHAR(MAX),
    @Type TINYINT,
    @Status TINYINT,
    @WaitTimeDays SMALLINT,
    @RecoveryInitiatedDate DATETIME2(7),
    @LastNotificationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[EmergencyAccess]
    SET
        [GrantorId] = @GrantorId,
        [GranteeId] = @GranteeId,
        [Email] = @Email,
        [KeyEncrypted] = @KeyEncrypted,
        [Type] = @Type,
        [Status] = @Status,
        [WaitTimeDays] = @WaitTimeDays,
        [RecoveryInitiatedDate] = @RecoveryInitiatedDate,
        [LastNotificationDate] = @LastNotificationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @GranteeId
END
GO

-- Recreate procedure EmergencyAccess_ReadCountByGrantorIdEmail
IF OBJECT_ID('[dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]
    @GrantorId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @OnlyUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[EmergencyAccess] EA
    LEFT JOIN
        [dbo].[User] U ON EA.[GranteeId] = U.[Id]
    WHERE
        EA.[GrantorId] = @GrantorId
        AND (
            (@OnlyUsers = 0 AND (EA.[Email] = @Email OR U.[Email] = @Email))
            OR (@OnlyUsers = 1 AND U.[Email] = @Email)
        )
END
GO