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

CREATE OR ALTER PROCEDURE [dbo].[Device_ReadActiveWithPendingAuthRequestsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.[Id],
        D.[UserId],
        D.[Name],
        D.[Type],
        D.[Identifier],
        D.[PushToken],
        D.[CreationDate],
        D.[RevisionDate],
        D.[EncryptedUserKey],
        D.[EncryptedPublicKey],
        D.[EncryptedPrivateKey],
        D.[Active],
        D.[LastActivityDate], 
        AR.[Id] AS [AuthRequestId],
        AR.[CreationDate] AS [AuthRequestCreationDate]
    FROM
        [dbo].[DeviceView] D
    LEFT OUTER JOIN (
        SELECT
            [Id],
            [CreationDate],
            [RequestDeviceIdentifier],
            [Approved],
            ROW_NUMBER() OVER (PARTITION BY [RequestDeviceIdentifier] ORDER BY [CreationDate] DESC) AS rn
        FROM
            [dbo].[AuthRequestView]
        WHERE
            [Type] IN (0,1)  -- AuthenticateAndUnlock and Unlock types only
            AND [CreationDate] >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE()) -- Ensure the request hasn't expired
            AND [UserId] = @UserId -- Requests for this user only
    ) AR -- This join will get the most recent request per device, regardless of approval status
    ON D.[Identifier] = AR.[RequestDeviceIdentifier] AND AR.[rn] = 1 AND AR.[Approved] IS NULL -- Get only the most recent unapproved request per device
    WHERE
        D.[UserId] = @UserId -- Include only devices for this user
        AND D.[Active] = 1; -- Include only active devices
END
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
        -- LastActivityDate only moves forward. Two scenarios could silently clobber a valid bump:
        --   1. NULL passthrough: a general save that does not intend to touch LastActivityDate passes NULL
        --      (the default); we must not overwrite an existing value with NULL.
        --   2. Stale non-null overwrite: a thread that loaded the device before a concurrent bump fires
        --      may call SaveAsync with an older date; we must not clobber the fresher DB value.
        -- The CASE expression handles both: LastActivityDate is updated only when the incoming value is
        -- strictly greater than the current DB value (ISNULL baseline of '1900-01-01' handles NULL DB values).
        [LastActivityDate] = CASE
            WHEN @LastActivityDate > ISNULL([LastActivityDate], '1900-01-01') THEN @LastActivityDate
            ELSE [LastActivityDate]
        END
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Device_UpdateLastActivityDateById]
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

CREATE OR ALTER PROCEDURE [dbo].[Device_UpdateLastActivityDateByIdentifierUserId]
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
