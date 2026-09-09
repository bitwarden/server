CREATE PROCEDURE [dbo].[Device_UpdateLastActivityById]
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
