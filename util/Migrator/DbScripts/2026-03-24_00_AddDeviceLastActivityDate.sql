IF COL_LENGTH('[dbo].[Device]', 'LastActivityDate') IS NULL
BEGIN
  ALTER TABLE
    [dbo].[Device]
  ADD
    [LastActivityDate] DATETIME2 (7) NULL
END
GO

CREATE OR ALTER VIEW [dbo].[DeviceView]
AS
    SELECT
        *
    FROM
        [dbo].[Device]
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @EncryptedUserKey VARCHAR(MAX) = NULL,
    @EncryptedPublicKey VARCHAR(MAX) = NULL,
    @EncryptedPrivateKey VARCHAR(MAX) = NULL,
    @Active BIT = 1,
    @LastActivityDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Device]
    (
        [Id],
        [UserId],
        [Name],
        [Type],
        [Identifier],
        [PushToken],
        [CreationDate],
        [RevisionDate],
        [EncryptedUserKey],
        [EncryptedPublicKey],
        [EncryptedPrivateKey],
        [Active],
        [LastActivityDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Type,
        @Identifier,
        @PushToken,
        @CreationDate,
        @RevisionDate,
        @EncryptedUserKey,
        @EncryptedPublicKey,
        @EncryptedPrivateKey,
        @Active,
        @LastActivityDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @EncryptedUserKey VARCHAR(MAX) = NULL,
    @EncryptedPublicKey VARCHAR(MAX) = NULL,
    @EncryptedPrivateKey VARCHAR(MAX) = NULL,
    @Active BIT = 1,
    @LastActivityDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Type] = @Type,
        [Identifier] = @Identifier,
        [PushToken] = @PushToken,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [EncryptedUserKey] = @EncryptedUserKey,
        [EncryptedPublicKey] = @EncryptedPublicKey,
        [EncryptedPrivateKey] = @EncryptedPrivateKey,
        [Active] = @Active,
        -- NULL preserves the existing value, preventing general updates from overwriting a recently-bumped
        -- LastActivityDate. Pass a non-NULL value to update it in the same write.
        [LastActivityDate] = COALESCE(@LastActivityDate, [LastActivityDate])
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_BumpLastActivityDateById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Only update if LastActivityDate has never been set or was last set on a prior calendar day.
    -- This acts as a fallback guard against redundant writes in case the application-layer cache
    -- is unavailable or evicted. In normal operation the cache
    -- prevents this procedure from being called more than once per device per day entirely.
    -- Product only requires day-level granularity (today / this week / last week / etc.).
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_BumpLastActivityDateByIdentifierAndUserId]
    @Identifier NVARCHAR(50),
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Both @Identifier and @UserId are required: Identifier is unique per user, not globally
    -- (unique constraint UX_Device_UserId_Identifier is on (UserId, Identifier)). Including
    -- UserId scopes the write to the authenticated user's device and ensures the query hits
    -- UX_Device_UserId_Identifier; without it the query falls back to IX_Device_Identifier,
    -- which is non-unique and would require a scan across all users.
    --
    -- Only update if LastActivityDate has never been set or was last set on a prior calendar day.
    -- This acts as a fallback guard against redundant writes in case the application-layer cache
    -- is unavailable or evicted. In normal operation the cache
    -- prevents this procedure from being called more than once per device per day entirely.
    -- Product only requires day-level granularity (today / this week / last week / etc.).
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] = GETUTCDATE()
    WHERE
        [Identifier] = @Identifier
        AND [UserId] = @UserId
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
        )
END
GO
