CREATE PROCEDURE [dbo].[Device_BumpDataByIdentifierUserId]
    @Identifier NVARCHAR(50),
    @UserId UNIQUEIDENTIFIER,
    @ClientVersion NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Both @Identifier and @UserId are required: Identifier is unique per user, not globally
    -- (unique constraint UX_Device_UserId_Identifier is on (UserId, Identifier)). Including
    -- UserId scopes the write to the authenticated user's device and ensures the query hits
    -- UX_Device_UserId_Identifier; without it the query falls back to IX_Device_Identifier,
    -- which is non-unique and would require a scan across all users.
    --
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
        [Identifier] = @Identifier
        AND [UserId] = @UserId
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
            OR (@ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion))
        )
END
