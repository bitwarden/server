/*
 * Add support for Emergency Access
 */
IF OBJECT_ID('[dbo].[EmergencyAccess]') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmergencyAccess] (
        [Id]                    UNIQUEIDENTIFIER NOT NULL,
        [GrantorId]             UNIQUEIDENTIFIER NOT NULL,
        [GranteeId]             UNIQUEIDENTIFIER NULL,
        [Email]                 NVARCHAR (50)    NULL,
        [KeyEncrypted]          VARCHAR (MAX)    NULL,
        [WaitTimeDays]          SMALLINT         NULL,
        [Type]                  TINYINT          NOT NULL,
        [Status]                TINYINT          NOT NULL,
        [RecoveryInitiatedDate] DATETIME2 (7)    NULL,
        [LastNotificationDate]  DATETIME2 (7)    NULL,
        [CreationDate]          DATETIME2 (7)    NOT NULL,
        [RevisionDate]          DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_EmergencyAccess] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    ALTER TABLE [dbo].[EmergencyAccess] WITH NOCHECK
        ADD CONSTRAINT [FK_EmergencyAccess_GrantorId] FOREIGN KEY ([GrantorId]) REFERENCES [dbo].[User] ([Id]);

    ALTER TABLE [dbo].[EmergencyAccess] WITH NOCHECK
        ADD CONSTRAINT [FK_EmergencyAccess_GranteeId] FOREIGN KEY ([GranteeId]) REFERENCES [dbo].[User] ([Id]);
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'EmergencyAccessDetailsView')
BEGIN
    DROP VIEW [dbo].[EmergencyAccessDetailsView]
END
GO

CREATE VIEW [dbo].[EmergencyAccessDetailsView]
AS
SELECT
    EA.*,
    GranteeU.[Name] GranteeName,
    ISNULL(GranteeU.[Email], EA.[Email]) GranteeEmail,
    GrantorU.[Name] GrantorName,
    GrantorU.[Email] GrantorEmail
FROM
    [dbo].[EmergencyAccess] EA
LEFT JOIN
    [dbo].[User] GranteeU ON GranteeU.[Id] = EA.[GranteeId]
LEFT JOIN
    [dbo].[User] GrantorU ON GrantorU.[Id] = EA.[GrantorId]
GO

IF OBJECT_ID('[dbo].[User_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[User_DeleteById]
@Id UNIQUEIDENTIFIER
    WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @Id

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] = @Id

    -- Delete U2F logins
    DELETE
    FROM
        [dbo].[U2f]
    WHERE
        [UserId] = @Id

    -- Delete SSO Users
    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] = @Id

    -- Delete Emergency Accesses
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [GrantorId] = @Id
    OR
        [GranteeId] = @Id

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccess_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_Create]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_Create]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
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

IF OBJECT_ID('[dbo].[EmergencyAccess_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_ReadById]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]
    @GrantorId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
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

IF OBJECT_ID('[dbo].[EmergencyAccess_ReadToNotify]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_ReadToNotify]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_ReadToNotify]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        EA.*,
        Grantee.Name as GranteeName,
        Grantor.Email as GrantorEmail
    FROM
        [dbo].[EmergencyAccess] EA
    LEFT JOIN
        [dbo].[User] Grantor ON Grantor.[Id] = EA.[GrantorId]
    LEFT JOIN
        [dbo].[User] Grantee On Grantee.[Id] = EA.[GranteeId]
    WHERE
        EA.[Status] = 3
    AND
        DATEADD(DAY, EA.[WaitTimeDays] - 1, EA.[RecoveryInitiatedDate]) <= GETUTCDATE()
    AND
        DATEADD(DAY, 1, EA.[LastNotificationDate]) <= GETUTCDATE()
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccess_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_Update]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_Update]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
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

IF OBJECT_ID('[dbo].[EmergencyAccessDetails_ReadByGranteeId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGranteeId]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGranteeId]
    @GranteeId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [GranteeId] = @GranteeId
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccessDetails_ReadByGrantorId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGrantorId]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGrantorId]
    @GrantorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [GrantorId] = @GrantorId
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccessDetails_ReadByIdGrantorId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccessDetails_ReadByIdGrantorId]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadByIdGrantorId]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [Id] = @Id
    AND
        [GrantorId] = @GrantorId
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccessDetails_ReadExpiredRecoveries]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccessDetails_ReadExpiredRecoveries]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadExpiredRecoveries]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [Status] = 3
    AND
        DATEADD(DAY, [WaitTimeDays], [RecoveryInitiatedDate]) <= GETUTCDATE()
END
GO

IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByEmergencyAccessGranteeId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByEmergencyAccessGranteeId]
END
GO

CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByEmergencyAccessGranteeId]
    @EmergencyAccessId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[EmergencyAccess] EA ON EA.[GranteeId] = U.[Id]
    WHERE
        EA.[Id] = @EmergencyAccessId
        AND EA.[Status] = 2 -- Confirmed
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccess_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[EmergencyAccess_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[User_BumpAccountRevisionDateByEmergencyAccessGranteeId] @Id
    
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [Id] = @Id
END
GO
