-- PM-37166 — Add ClientVersion column to Device and refactor the LastActivityDate update pathway
-- into a combined "UpdateLastActivity" pathway that handles both columns in a single DB round trip.
-- "Last activity" names the event of the device's most recent appearance; LastActivityDate and
-- ClientVersion are the facts we observed about that event. See IUpdateDeviceLastActivityCommand
-- for the contract-level extensibility note (additional last-observed properties slot in here).

-- 1. Add the ClientVersion column. Guarded so reruns are safe.
IF COL_LENGTH('[dbo].[Device]', 'ClientVersion') IS NULL
BEGIN
    ALTER TABLE [dbo].[Device]
        ADD [ClientVersion] NVARCHAR(43) NULL
END
GO

-- 2. DeviceView mirrors all columns — refresh so it picks up ClientVersion.
CREATE OR ALTER VIEW [dbo].[DeviceView]
AS
    SELECT
        *
    FROM
        [dbo].[Device]
GO

-- 2a. Refresh read sprocs that consume DeviceView so their cached schema picks up ClientVersion.
-- The write sprocs (Device_Create / Device_Update / Device_UpdateLastActivity*) are recreated below,
-- so they pick up the new column naturally and don't need a refresh.
IF OBJECT_ID('[dbo].[Device_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Device_ReadById]'
END
GO

IF OBJECT_ID('[dbo].[Device_ReadByIdentifierUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Device_ReadByIdentifierUserId]'
END
GO

IF OBJECT_ID('[dbo].[Device_ReadByUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Device_ReadByUserId]'
END
GO

IF OBJECT_ID('[dbo].[Device_ReadByIdentifier]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Device_ReadByIdentifier]'
END
GO

-- 3. Device_Create: accept @ClientVersion and include it in the INSERT list.
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
    @LastActivityDate DATETIME2(7) = NULL,
    @ClientVersion NVARCHAR(43) = NULL
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
        [LastActivityDate],
        [ClientVersion]
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
        @LastActivityDate,
        @ClientVersion
    )
END
GO

-- 4. Device_Update: accept @ClientVersion with NULL-passthrough guard.
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
    @LastActivityDate DATETIME2(7) = NULL,
    @ClientVersion NVARCHAR(43) = NULL
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
        -- LastActivityDate only moves forward. Two scenarios could silently clobber a valid update:
        --   1. NULL passthrough: a general save that does not intend to touch LastActivityDate passes NULL
        --      (the default); we must not overwrite an existing value with NULL.
        --   2. Stale non-null overwrite: a thread that loaded the device before a concurrent
        --      last-activity update fires may call SaveAsync with an older date; we must not
        --      clobber the fresher DB value.
        -- The CASE expression handles both: LastActivityDate is updated only when the incoming value is
        -- strictly greater than the current DB value (ISNULL baseline of '1900-01-01' handles NULL DB values).
        [LastActivityDate] = CASE
            WHEN @LastActivityDate > ISNULL([LastActivityDate], '1900-01-01') THEN @LastActivityDate
            ELSE [LastActivityDate]
        END,
        -- ClientVersion is value-equality based, not forward-only — downgrades are valid (e.g. a user
        -- reverts a desktop install). We only need NULL passthrough so unrelated SaveAsync calls (that
        -- don't intend to touch ClientVersion) don't clobber the stored value with NULL.
        [ClientVersion] = ISNULL(@ClientVersion, [ClientVersion])
    WHERE
        [Id] = @Id
END
GO

-- 5. Device_UpdateLastActivityById: new combined SP. Replaces Device_UpdateLastActivityDateById.
CREATE OR ALTER PROCEDURE [dbo].[Device_UpdateLastActivityById]
    @Id UNIQUEIDENTIFIER,
    @LastActivityDate DATETIME2(7),
    @ClientVersion NVARCHAR(43) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- "Last activity" names the *event* of the device's most recent appearance, not just one column.
    -- The fields written here are the set of facts we observed about that event:
    --   LastActivityDate — when it occurred (day-level idempotence: only move forward to today).
    --   ClientVersion    — what the client was running at the time (value-level idempotence: only
    --                       write when @ClientVersion is non-null and differs from the stored value).
    -- ClientVersion is treated as a property of the activity event rather than an independent value.
    -- Additional last-observed properties (e.g. last IP, OS) would slot in here without renaming.
    -- See IUpdateDeviceLastActivityCommand for the contract-level extensibility note.
    --
    -- One UPDATE handles both columns. The WHERE clause ensures we only issue a write when at least
    -- one column actually needs changing. The application-layer cache is the primary protection
    -- against redundant calls; these SP-side guards are a safety net in case the cache is unavailable
    -- or evicted.
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] =
            CASE
                WHEN [LastActivityDate] IS NULL OR CAST([LastActivityDate] AS DATE) < CAST(@LastActivityDate AS DATE)
                    THEN @LastActivityDate
                ELSE [LastActivityDate]
            END,
        [ClientVersion] =
            CASE
                WHEN @ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion)
                    THEN @ClientVersion
                ELSE [ClientVersion]
            END
    WHERE
        [Id] = @Id
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(@LastActivityDate AS DATE)
            OR (@ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion))
        )
END
GO

-- 6. Device_UpdateLastActivityByIdentifierUserId: new combined SP. Replaces
-- Device_UpdateLastActivityDateByIdentifierUserId.
CREATE OR ALTER PROCEDURE [dbo].[Device_UpdateLastActivityByIdentifierUserId]
    @Identifier NVARCHAR(50),
    @UserId UNIQUEIDENTIFIER,
    @LastActivityDate DATETIME2(7),
    @ClientVersion NVARCHAR(43) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Both @Identifier and @UserId are required: Identifier is unique per user, not globally
    -- (unique constraint UX_Device_UserId_Identifier is on (UserId, Identifier)). Including
    -- UserId scopes the write to the authenticated user's device and ensures the query hits
    -- UX_Device_UserId_Identifier; without it the query falls back to IX_Device_Identifier,
    -- which is non-unique and would require a scan across all users.
    --
    -- See Device_UpdateLastActivityById for the event-oriented naming rationale and per-column guards.
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] =
            CASE
                WHEN [LastActivityDate] IS NULL OR CAST([LastActivityDate] AS DATE) < CAST(@LastActivityDate AS DATE)
                    THEN @LastActivityDate
                ELSE [LastActivityDate]
            END,
        [ClientVersion] =
            CASE
                WHEN @ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion)
                    THEN @ClientVersion
                ELSE [ClientVersion]
            END
    WHERE
        [Identifier] = @Identifier
        AND [UserId] = @UserId
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(@LastActivityDate AS DATE)
            OR (@ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion))
        )
END
GO

-- Note: the old single-column SPs (Device_UpdateLastActivityDateById /
-- Device_UpdateLastActivityDateByIdentifierUserId) are intentionally NOT dropped here.
-- Server and DB deploys are decoupled, so the server could roll back to a version that
-- still calls the old SPs after this migration has run. A follow-up ticket will drop
-- them in a later migration once we're confident no deployed server version calls them.
