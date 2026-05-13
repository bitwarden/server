CREATE PROCEDURE [dbo].[Device_BumpDataById]
    @Id UNIQUEIDENTIFIER,
    @ClientVersion NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- One UPDATE handles both columns. The WHERE clause ensures we only issue a write when at least
    -- one column actually needs changing. Each SET expression independently guards its column:
    --   LastActivityDate: day-level idempotence (only move forward to today).
    --   ClientVersion:    value-level idempotence (only write when @ClientVersion is non-null and differs).
    -- The application-layer cache is the primary protection against redundant calls; these SP-side
    -- guards are a safety net in case the cache is unavailable or evicted.
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] =
            CASE
                WHEN [LastActivityDate] IS NULL OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
                    THEN GETUTCDATE()
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
            OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
            OR (@ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion))
        )
END
